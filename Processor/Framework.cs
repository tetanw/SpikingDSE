using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SpikingDSE
{
    [DebuggerStepThrough]
    class SimThread : IComparable<SimThread>
    {
        public Process Process;
        public List<IEnumerator<Command>> Runnables;
        public int Time;
        public object message;

        public int CompareTo([AllowNull] SimThread other)
        {
            return Time.CompareTo(other.Time);
        }
    }

    class Channel
    {
        public object Message;
        public int Time;
        public SimThread Sender;
        public SimThread Receiver;
    }

    public class Scheduler
    {
        private PriorityQueue<SimThread> running = new PriorityQueue<SimThread>();
        private List<Channel> channelReg = new List<Channel>();
        private List<Process> processes = new List<Process>();
        private Environment env;

        public Scheduler()
        {
            this.env = new Environment();
        }

        public T AddProcess<T>(T process) where T : Process
        {
            processes.Add(process);
            return process;
        }

        public void AddChannel(ref Port a, ref Port b)
        {
            if (a.Handle != 0 || b.Handle != 0)
            {
                throw new Exception("Port already bound");
            }

            var channel = new Channel
            {
                Message = null,
                Sender = null,
                Receiver = null
            };
            channelReg.Add(channel);
            int newId = channelReg.Count;
            a.Handle = newId;
            b.Handle = newId;
        }

        public void Init()
        {
            foreach (var process in processes)
            {
                process.Init(env);
                running.Enqueue(new SimThread
                {
                    Process = process,
                    Runnables = new List<IEnumerator<Command>>() { process.Run().GetEnumerator() },
                    Time = 0
                });
            }
        }

        public int RunUntil(int stopTime, int stopCmds)
        {
            int nrCommands = 0;
            while (running.Count > 0 && nrCommands < stopCmds)
            {
                nrCommands++;
                var thread = running.Dequeue();
                if (thread.Time > stopTime)
                    break;

                env.Now = thread.Time;
                env.Received = thread.message;
                var runnable = thread.Runnables[thread.Runnables.Count - 1];
                bool stillRunning = runnable.MoveNext();
                if (!stillRunning)
                {
                    if (thread.Runnables.Count > 0)
                    {
                        thread.Runnables.RemoveAt(thread.Runnables.Count - 1);
                        running.Enqueue(thread);
                    }
                    else
                    {
                        continue;
                    }
                }

                var cmd = runnable.Current;
                if (cmd is SleepCmd)
                {
                    var sleep = cmd as SleepCmd;
                    thread.Time += sleep.Time;
                    running.Enqueue(thread);
                }
                else if (cmd is SendCmd)
                {
                    var send = cmd as SendCmd;
                    var channel = channelReg[send.Port.Handle - 1];
                    channel.Sender = thread;
                    channel.Message = send.Message;
                    channel.Time = send.Time;
                    PollTransmitMessage(channel);
                }
                else if (cmd is ReceiveCmd)
                {
                    var recv = cmd as ReceiveCmd;
                    var channel = channelReg[recv.Port.Handle - 1];
                    channel.Receiver = thread;
                    PollTransmitMessage(channel);
                }
                else if (cmd is ProcessCmd)
                {
                    var process = cmd as ProcessCmd;
                    thread.Runnables.Add(process.Runnable.GetEnumerator());
                    running.Enqueue(thread);
                }
            }

            return nrCommands;
        }

        private void PollTransmitMessage(Channel channel)
        {
            if (channel.Sender != null && channel.Receiver != null)
            {
                channel.Sender.Time = channel.Time;
                channel.Receiver.Time = channel.Time;
                channel.Receiver.message = channel.Message;
                running.Enqueue(channel.Sender);
                running.Enqueue(channel.Receiver);
                channel.Sender = null;
                channel.Receiver = null;
                channel.Message = null;
            }
        }
    }

    public enum Dir
    {
        Out,
        In
    }

    public abstract class Process
    {
        protected Environment env;

        public void Init(Environment env)
        {
            this.env = env;
        }

        public abstract IEnumerable<Command> Run();
    }

    public class Command
    {

    }

    public class SleepCmd : Command
    {
        public int Time;
    }

    public class SendCmd : Command
    {
        public Port Port;
        public object Message;
        public int Time;
    }

    public class ReceiveCmd : Command
    {
        public Port Port;
    }

    public class ProcessCmd : Command
    {
        public IEnumerable<Command> Runnable;
    }

    [DebuggerStepThrough]
    public class Environment
    {
        public Command Delay(int time)
        {
            return new SleepCmd { Time = time };
        }

        public Command SleepUntil(int newTime)
        {
            return new SleepCmd { Time = newTime - Now };
        }

        public Command Send(Port port, object message)
        {
            return new SendCmd { Port = port, Message = message, Time = Now };
        }

        public Command SendAt(Port port, object message, int time)
        {
            return new SendCmd { Port = port, Message = message, Time = time };
        }

        public Command Receive(Port port)
        {
            return new ReceiveCmd { Port = port };
        }

        public Command WaitFor(IEnumerable<Command> process)
        {
            return new ProcessCmd { Runnable = process };
        }


        public bool Ready(Port port)
        {
            // TODO: Implement
            return true;
        }

        public object Received
        {
            get; set;
        }

        public int Now { get; set; }
    }

    public class Port
    {
        public int Handle;
    }
}