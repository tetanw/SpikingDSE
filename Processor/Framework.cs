using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SpikingDSE
{
    public class SimThread : IComparable<SimThread>
    {
        public IEnumerator<Command> Runnable;
        public long Time;

        public int CompareTo([AllowNull] SimThread other)
        {
            return Time.CompareTo(other.Time);
        }
    }

    class Channel
    {
        public string Name;
        public OutPort OutPort;
        public InPort InPort;

        public SimThread SendThread;
        public SendCmd SendCmd;
        public SimThread ReceiveThread;
        public Command ReceiveCmd;
    }

    public class Simulator
    {
        private PriorityQueue<SimThread> ready = new PriorityQueue<SimThread>();
        private List<Channel> channels = new List<Channel>();
        private List<Actor> actors = new List<Actor>();
        private Environment env;

        public Simulator()
        {
            this.env = new Environment();
        }

        public T AddProcess<T>(T process) where T : Actor
        {
            actors.Add(process);
            return process;
        }

        public void AddChannel(ref InPort inPort, ref OutPort outPort, string name = null)
        {
            if (inPort != null || outPort != null)
            {
                throw new Exception("Port already bound");
            }

            inPort = new InPort();
            outPort = new OutPort();
            var channel = new Channel
            {
                OutPort = outPort,
                InPort = inPort,
                Name = name
            };
            int newId = channels.Count;
            channels.Add(channel);
            inPort.ChannelHandle = newId;
            outPort.ChannelHandle = newId;
        }

        public void AddChannel(ref OutPort outPort, ref InPort inPort, string name = null)
        {
            AddChannel(ref inPort, ref outPort, name);
        }

        public void Init()
        {
            foreach (var actor in actors)
            {
                actor.Init(env);
                ready.Enqueue(new SimThread
                {
                    Runnable = actor.Run().GetEnumerator(),
                    Time = 0
                });
            }
        }

        public int RunUntil(int stopTime, int stopCmds)
        {
            int nrCommands = 0;
            while (ready.Count > 0 && nrCommands < stopCmds)
            {
                nrCommands++;
                var currentThread = ready.Dequeue();
                if (currentThread.Time > stopTime)
                    break;

                env.Now = currentThread.Time;
                var runnable = currentThread.Runnable;
                bool stillRunning = runnable.MoveNext();
                if (!stillRunning)
                {
                    continue;
                }

                var cmd = runnable.Current;
                switch (cmd)
                {
                    case SleepCmd sleep:
                        {
                            currentThread.Time += sleep.Time;
                            ready.Enqueue(currentThread);
                            break;
                        }
                    case SendCmd send:
                        {
                            var channel = channels[send.Port.ChannelHandle];
                            if (channel.SendCmd != null)
                            {
                                throw new Exception("Channel is already occupied");
                            }

                            channel.SendCmd = send;
                            channel.SendThread = currentThread;
                            if (channel.ReceiveCmd != null)
                            {
                                DoChannelTransfer(channel);
                            }
                            break;
                        }
                    case ReceiveCmd recv:
                        {
                            var channel = channels[recv.Port.ChannelHandle];
                            if (channel.ReceiveCmd != null)
                            {
                                throw new Exception("Channel is already occupied");
                            }

                            channel.ReceiveCmd = recv;
                            channel.ReceiveThread = currentThread;
                            if (channel.SendCmd != null)
                            {
                                DoChannelTransfer(channel);
                            }
                            break;
                        }
                    case SelectCmd select:
                        {
                            for (int i = 0; i < select.Ports.Length; i++)
                            {
                                var port = select.Ports[i];
                                if (port == null)
                                {
                                    continue;
                                }

                                var aChannel = channels[port.ChannelHandle];
                                aChannel.ReceiveCmd = select;
                                aChannel.ReceiveThread = currentThread;
                                if (aChannel.SendCmd != null)
                                {
                                    DoChannelTransfer(aChannel);
                                    break;
                                }
                            }

                            break;
                        }
                    case ParCmd parallel:
                        {
                            // FIXME: Am I going to do it like this?
                            foreach (var process in parallel.Processes)
                            {
                                ready.Enqueue(new SimThread
                                {
                                    Runnable = process.GetEnumerator(),
                                    Time = currentThread.Time
                                });
                            }
                            break;
                        }
                    default:
                        throw new Exception("Unknown command: " + cmd);
                }
            }

            return nrCommands;
        }

        private void DoChannelTransfer(Channel channel)
        {
            if (channel.ReceiveCmd is ReceiveCmd)
            {
                var rcv = channel.ReceiveCmd as ReceiveCmd;
                rcv.Message = channel.SendCmd.Message;
                long newTime = Math.Max(channel.SendCmd.Time, rcv.Time);
                QueueThreads(channel, newTime);
                CleanChannel(channel);
            }
            else if (channel.ReceiveCmd is SelectCmd)
            {
                var select = channel.ReceiveCmd as SelectCmd;
                select.Message = channel.SendCmd.Message;
                select.Port = channel.InPort;
                long newTime = Math.Max(channel.SendCmd.Time, select.Time);
                QueueThreads(channel, newTime);

                for (int i = 0; i < select.Ports.Length; i++)
                {
                    var port = select.Ports[i];
                    if (port == null)
                        continue;
                    var aChannel = channels[port.ChannelHandle];
                    CleanChannel(aChannel);
                }
            }
            else
            {
                throw new Exception("Unknown receive command");
            }

        }

        private void CleanChannel(Channel channel)
        {
            channel.SendCmd = null;
            channel.SendThread = null;
            channel.ReceiveCmd = null;
            channel.ReceiveThread = null;
        }

        private void QueueThreads(Channel channel, long newTime)
        {
            channel.SendThread.Time = newTime;
            channel.ReceiveThread.Time = newTime;
            ready.Enqueue(channel.SendThread);
            ready.Enqueue(channel.ReceiveThread);
        }
    }

    public abstract class Actor
    {
        protected Environment env;
        protected string name;

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
        // To simulator
        public long Time;
    }

    public class SendCmd : Command
    {
        // To simulator
        public OutPort Port;
        public object Message;

        // Result
        public long Time;
    }

    public class ReceiveCmd : Command
    {
        // To simulator
        public InPort Port;
        public long Time;

        // Result
        public object Message;
    }

    public class SelectCmd : Command
    {
        // To simulator
        public InPort[] Ports;
        public long Time;

        // Result
        public InPort Port;
        public object Message;
    }

    public class ParCmd : Command
    {
        // To simulator
        public IEnumerable<Command>[] Processes;
    }

    public class Environment
    {
        public SleepCmd Delay(long time)
        {
            return new SleepCmd { Time = time };
        }

        public SleepCmd SleepUntil(long newTime)
        {
            return new SleepCmd { Time = newTime - Now };
        }

        public SendCmd Send(OutPort port, object message)
        {
            return new SendCmd { Port = port, Message = message, Time = Now };
        }

        public SendCmd SendAt(OutPort port, object message, long time)
        {            
            return new SendCmd { Port = port, Message = message, Time = time };
        }

        public ReceiveCmd Receive(InPort port, long waitBefore = 0)
        {
            return new ReceiveCmd { Port = port, Time = Now + waitBefore };
        }

        public SelectCmd Select(params InPort[] ports)
        {
            return new SelectCmd { Ports = ports, Time = Now };
        }

        public ParCmd Parallel(params IEnumerable<Command>[] processes)
        {
            return new ParCmd { Processes = processes };
        }

        public long Now { get; set; }
    }

    public class Port
    {
        public int ChannelHandle;
    }

    public class InPort : Port
    {

    }

    public class OutPort : Port
    {

    }
}