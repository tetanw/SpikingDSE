using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

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
    }

    public class Scheduler
    {
        private PriorityQueue<SimThread> ready = new PriorityQueue<SimThread>();
        private List<Channel> channelReg = new List<Channel>();
        private List<Actor> actors = new List<Actor>();
        private Environment env;

        public Scheduler()
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

            inPort = new InPort
            {
                WaitingForRecv = null
            };
            outPort = new OutPort()
            {
                WaitingForSend = null
            };
            var channel = new Channel
            {
                OutPort = outPort,
                InPort = inPort,
                Name = name
            };
            channelReg.Add(channel);
            int newId = channelReg.Count;
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
                            var channel = channelReg[send.Port.ChannelHandle - 1];
                            if (channel.InPort.WaitingForRecv != null)
                            {
                                throw new Exception("In port is already busy");
                            }

                            // New
                            if (channel.OutPort.WaitingForSend != null)
                            {
                                if (channel.OutPort.WaitingForSend is ReceiveCmd)
                                {
                                    var recv = (ReceiveCmd)channel.OutPort.WaitingForSend;
                                    channel.OutPort.WaitingForSend = null;

                                    // Find the two channels plan
                                    SimThread sendThread = currentThread;
                                    SimThread recvThread = recv.Thread;
                                    recv.Thread = null;
                                    long newTime = Math.Max(send.Time, recv.Time);
                                    sendThread.Time = newTime;
                                    recvThread.Time = newTime;
                                    ready.Enqueue(sendThread);
                                    ready.Enqueue(recvThread);
                                    recv.Message = send.Message;
                                }
                                else if (channel.OutPort.WaitingForSend is SelectCmd)
                                {
                                    // TODO: May not always be a receive command
                                    var select = (SelectCmd)channel.OutPort.WaitingForSend;
                                    channel.OutPort.WaitingForSend = null;

                                    for (int i = 0; i < select.Ports.Length; i++)
                                    {
                                        var port = select.Ports[i];
                                        var selChannel = channelReg[port.ChannelHandle];
                                        selChannel.OutPort.WaitingForSend = null;
                                    }

                                    SimThread sendThread = currentThread;
                                    SimThread recvThread = select.Thread;
                                    send.Thread = null;
                                    long newTime = Math.Max(send.Time, select.Time);
                                    sendThread.Time = newTime;
                                    recvThread.Time = newTime;
                                    ready.Enqueue(sendThread);
                                    ready.Enqueue(recvThread);
                                    select.Message = send.Message;
                                    select.Port = channel.InPort;
                                }
                            }
                            else
                            {
                                send.Thread = currentThread;
                                channel.InPort.WaitingForRecv = send;
                            }

                            break;
                        }
                    case ReceiveCmd recv:
                        {
                            var channel = channelReg[recv.Port.ChannelHandle - 1];
                            if (channel.OutPort.WaitingForSend != null)
                            {
                                throw new Exception("Out port is already busy");
                            }

                            // New
                            if (channel.InPort.WaitingForRecv != null)
                            {
                                var send = (SendCmd)channel.InPort.WaitingForRecv;
                                channel.InPort.WaitingForRecv = null;

                                // Find the two channels plan
                                SimThread sendThread = send.Thread;
                                SimThread recvThread = currentThread;
                                send.Thread = null;
                                long newTime = Math.Max(send.Time, recv.Time);
                                sendThread.Time = newTime;
                                recvThread.Time = newTime;
                                ready.Enqueue(sendThread);
                                ready.Enqueue(recvThread);
                                recv.Message = send.Message;
                            }
                            else
                            {
                                recv.Thread = currentThread;
                                channel.OutPort.WaitingForSend = recv;
                            }

                            break;
                        }
                    case SelectCmd select:
                        {
                            for (int i = 0; i < select.Ports.Length; i++)
                            {
                                var recvPort = select.Ports[i];
                                var channel = channelReg[recvPort.ChannelHandle];

                                if (channel.InPort.WaitingForRecv != null)
                                {
                                    var send = (SendCmd)channel.InPort.WaitingForRecv;
                                    SimThread sendThread = send.Thread;
                                    SimThread recvThread = currentThread;
                                    send.Thread = null;
                                    long newTime = Math.Max(send.Time, select.Time);
                                    sendThread.Time = newTime;
                                    recvThread.Time = newTime;
                                    ready.Enqueue(sendThread);
                                    ready.Enqueue(recvThread);
                                    select.Message = send.Message;
                                    select.Port = recvPort;
                                }
                            }

                            select.Thread = currentThread;
                            for (int i = 0; i < select.Ports.Length; i++)
                            {
                                var recvPort = select.Ports[i];
                                var channel = channelReg[recvPort.ChannelHandle - 1];

                                channel.OutPort.WaitingForSend = select;
                            }

                            break;
                        }
                    case ParCmd parallel:
                        {
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
    }

    public abstract class Actor
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
        // Scheduler tracking variable, DO NOT USE
        // FIXME: I am not happy with this variable
        public SimThread Thread;
    }

    public class SleepCmd : Command
    {
        // To scheduler
        public long Time;
    }

    public class SendCmd : Command
    {
        // To scheduler
        public OutPort Port;
        public object Message;

        // Result
        public long Time;
    }

    public class ReceiveCmd : Command
    {
        // To scheduler
        public InPort Port;
        public long Time;

        // Result
        public object Message;
    }

    public class SelectCmd : Command
    {
        // To scheduler
        public InPort[] Ports;
        public long Time;

        // Result
        public InPort Port;
        public object Message;
    }

    public class ParCmd : Command
    {
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
        // FIXME: Not happy with this variable
        public Command WaitingForRecv;
    }

    public class OutPort : Port
    {
        // FIXME: Not happy with this variable
        public Command WaitingForSend;
    }

    public class Weights
    {
        public static double[,] ReadFromCSV(string path)
        {
            double[,] weights = null;
            int currentLine = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                double[] numbers = line.Split(",").Select(t => double.Parse(t)).ToArray();
                if (weights == null)
                {
                    weights = new double[numbers.Length, numbers.Length];
                }

                for (int i = 0; i < numbers.Length; i++)
                {
                    weights[i, currentLine] = numbers[i];
                }
                currentLine++;
            }

            return weights;
        }
    }
}