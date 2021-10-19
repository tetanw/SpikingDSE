using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace SpikingDSE
{
[DebuggerStepThrough]
    class SimThread : IComparable<SimThread>
    {
        public Process actor;
        public IEnumerator<Command> runnable;
        public int time;
        public object message;

        public int CompareTo([AllowNull] SimThread other)
        {
            return time.CompareTo(other.time);
        }
    }

    class Channel
    {
        public object Message;
        public SimThread Sender;
        public SimThread Receiver;
    }

    [DebuggerStepThrough]
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

        public T AddProcess<T>(T process) where T: Process
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
            foreach (var actor in processes)
            {
                actor.Init(env);
                running.Enqueue(new SimThread
                {
                    actor = actor,
                    runnable = actor.Run(),
                    time = 0
                });
            }
        }

        public int RunUntil(int stopTime)
        {
            int nrCommands = 0;
            while (running.Count > 0)
            {
                nrCommands++;
                var thread = running.Dequeue();
                if (thread.time > stopTime)
                    break;

                env.Now = thread.time;
                env.Received = thread.message;
                bool stillRunning = thread.runnable.MoveNext();
                if (!stillRunning)
                {
                    continue;
                }

                var cmd = thread.runnable.Current;
                if (cmd is SleepCmd)
                {
                    var sleep = cmd as SleepCmd;
                    thread.time += sleep.Time;
                    running.Enqueue(thread);
                }
                else if (cmd is SendCmd)
                {
                    var send = cmd as SendCmd;
                    var channel = channelReg[send.Port.Handle - 1];
                    channel.Sender = thread;
                    channel.Message = send.Message;
                    PollTransmitMessage(channel);
                }
                else if (cmd is ReceiveCmd)
                {
                    var recv = cmd as ReceiveCmd;
                    var channel = channelReg[recv.Port.Handle - 1];
                    channel.Receiver = thread;
                    PollTransmitMessage(channel);
                }
            }

            return nrCommands;
        }

        private void PollTransmitMessage(Channel channel)
        {
            if (channel.Sender != null && channel.Receiver != null)
            {
                channel.Sender.time = env.Now;
                channel.Receiver.time = env.Now;
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

        public abstract IEnumerator<Command> Run();
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
    }

    public class ReceiveCmd : Command
    {
        public Port Port;
    }

    [DebuggerStepThrough]
    public class Environment
    {
        public SleepCmd Delay(int time)
        {
            return new SleepCmd { Time = time };
        }

        public SendCmd Send(Port port, object message)
        {
            return new SendCmd { Port = port, Message = message };
        }

        public ReceiveCmd Receive(Port port)
        {
            return new ReceiveCmd { Port = port };
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