using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class ClusterStateManager
    {
        public State State;
        public Event Event;
        public long PeerEpiry;
        public static long HeartBeat = 1000;

        public bool StateMachine()
        {
            bool exception = false;

            if (State == State.StatePrimary)
            {
                if (Event == Event.PeerBackup)
                {
                    Console.WriteLine("I: connected to backup (passive), ready active.");
                    State = State.StateActive;
                }
                else if (Event == Event.PeerActive)
                {
                    Console.WriteLine("I: connected to backup (active), ready passive.");
                    State = State.StatePassive;
                }
            }
            else if (State == State.StateBackup)
            {
                if (Event == Event.PeerActive)
                {
                    Console.WriteLine("I: connected to primary (active), ready pasive.");
                    State = State.StatePassive;
                }
                else
                {
                    if (Event == Event.ClientRequest)
                        exception = true;
                } 
            }
            else if (State == State.StateActive)
            {
                if (Event == Event.PeerActive)
                {
                    Console.WriteLine("E: fatal error - dual actives, aborting");
                    exception = true;
                }
            }
            else if (State == State.StatePassive)
            {
                if (Event == Event.PeerPrimary)
                {
                    Console.WriteLine("I: primary (passive) is restarting, ready active");
                    State = State.StateActive;
                }
                else if (Event == Event.PeerBackup)
                {
                    Console.WriteLine("I: backup (passive) is restarting, ready active");
                    State = State.StateActive;
                }
                else if (Event == Event.PeerPassive)
                {
                    Console.WriteLine("E: fatal error - dual passives, aborting");
                    exception = true;
                }
                else if (Event == Event.ClientRequest)
                {
                    Debug.Assert(PeerEpiry > 0);
                    if (Environment.TickCount > PeerEpiry)
                    {
                        Console.WriteLine("I: failover successful, ready active");
                        State = State.StateActive;
                    }
                    else
                    {
                        exception = true;
                    }
                }
            }

            return exception;
        }
    }

    public enum State
    {
        StatePrimary,
        StateBackup,
        StateActive,
        StatePassive
    }

    public enum Event
    {
        PeerPrimary,
        PeerBackup,
        PeerActive,
        PeerPassive,
        ClientRequest
    }
}
