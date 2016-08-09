using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

namespace Client
{
    public class Program
    {
        private static int _requestTimeout = 1000;
        private static int _settleDelay = 2000;

        public static void Main(string[] args)
        {
            string[] server =
            {
                "tcp://127.0.0.1:5001",
                "tcp://127.0.0.1:5002"
            };
            int serverNbr = 0;

            Console.WriteLine("I: connecting to server at " + serverNbr);

            using (var ctx = new ZContext())
            {
                    var client = new ZSocket(ctx, ZSocketType.REQ);
                    client.Connect(server[serverNbr]);
                    long sequence = 0;
                
                    var poll = ZPollItem.CreateReceiver();

                    while (true)
                    {
                        bool expectReply = true;
                    
                        while (expectReply)
                        {
                            var frame = ZFrame.Create(32);
                            frame.Write(++sequence);
                            client.Send(frame);
                            ZError error;
                            ZMessage incoming;

                            if (client.PollIn(poll, out incoming, out error, TimeSpan.FromMilliseconds(_requestTimeout)))
                            {
                                using (incoming)
                                {
                                    int respseq = incoming[0].ReadInt32();
                                    if (respseq == sequence)
                                    {
                                        Console.WriteLine("I: server replied OK");
                                        expectReply = false;
                                        Thread.Sleep(1000);
                                    }
                                    else
                                    {
                                        Console.WriteLine("E: bad reply from server");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("W: no response from server, failing over");
                                client.Disconnect(server[serverNbr]);
                                client.Close();
                                serverNbr = (serverNbr + 1) % 2;
                                Thread.Sleep(_settleDelay);
                                Console.WriteLine("I: connecting to server at " + server[serverNbr]);
                                client = new ZSocket(ctx, ZSocketType.REQ);
                                client.Connect(server[serverNbr]);
                            }
                        }                            
                    }
                }
        }
    }
}
