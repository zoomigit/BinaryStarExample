using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

namespace Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using (var ctx = ZContext.Create())
            {
                using (var statepub = new ZSocket(ctx, ZSocketType.PUB))
                {
                    using (var statesub = new ZSocket(ctx, ZSocketType.SUB))
                    {
                        using (var frontend = new ZSocket(ctx, ZSocketType.ROUTER))
                        {
                            var csm = new ClusterStateManager();
                            if (args.Length == 1 && args[0].Equals("-p"))
                            {
                                Console.WriteLine("I: Primary active, waiting for backup (passive)");
                                frontend.Bind("tcp://127.0.0.1:5001");
                                statepub.Bind("tcp://127.0.0.1:5003");
                                statesub.Connect("tcp://127.0.0.1:5004");

                                csm.State = State.StatePrimary;
                            }
                            else if (args.Length == 1 && args[0].Equals("-b"))
                            {
                                Console.WriteLine("I: Backup passive, waiting for primary (active)");
                                frontend.Bind("tcp://127.0.0.1:5002");
                                statepub.Bind("tcp://127.0.0.1:5004");
                                statesub.Connect("tcp://127.0.0.1:5003");

                                csm.State = State.StateBackup;
                            }
                            else
                            {
                                Console.WriteLine("Usage: server { -p | -b }");
                            }

                            statesub.SetOption(ZSocketOption.SUBSCRIBE, "");
                            statesub.Subscribe("state");

                            long sendStateAt = Environment.TickCount + ClusterStateManager.HeartBeat;
                            var poll = ZPollItem.CreateReceiver();

                            while (true)
                            {
                                int timeLeft = (int)(sendStateAt - Environment.TickCount);

                                if (timeLeft < 0)
                                    timeLeft = 0;

                                ZError error;
                                ZMessage incoming;

                                if (statesub.PollIn(poll, out incoming, out error, TimeSpan.FromMilliseconds(timeLeft)))
                                {
                                    using (incoming)
                                    {
                                        csm.Event = (Event)incoming[0].ReadInt32();
                                        if (csm.StateMachine())
                                            break;
                                        csm.PeerEpiry = Environment.TickCount + 2 * ClusterStateManager.HeartBeat;
                                    }
                                }

                                if (frontend.PollIn(poll, out incoming, out error, TimeSpan.FromMilliseconds(timeLeft)))
                                {
                                    using (incoming)
                                    {
                                        csm.Event = Event.ClientRequest;
                                        if (csm.StateMachine() == false)
                                        {
                                            frontend.Send(incoming);
                                        }
                                    }
                                }

                                if (Environment.TickCount >= sendStateAt)
                                {
                                    var frame = ZFrame.Create(4);
                                    frame.Write((int)csm.State);
                                    statepub.Send(frame);

                                    sendStateAt = Environment.TickCount + ClusterStateManager.HeartBeat;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
