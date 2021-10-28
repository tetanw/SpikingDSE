using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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

        public override string ToString()
        {
            return Name;
        }
    }

    public class Resource
    {
        public int Handle;
        public int Amount;
    }

    class ResourceBlockage
    {
        public List<(ResDecreaseCmd, SimThread)> Waiting = new List<(ResDecreaseCmd, SimThread)>();
        public Resource Resource;
    }

    public class Simulator
    {
        private PriorityQueue<SimThread> ready = new PriorityQueue<SimThread>();
        private List<Channel> channels = new List<Channel>();
        private List<ResourceBlockage> resourceBlockages = new List<ResourceBlockage>();
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

        public void AddChannel(ref InPort inPort, ref OutPort outPort)
        {
            if (inPort != null || outPort != null)
            {
                throw new Exception("Port already bound");
            }

            inPort = new InPort() { IsBound = true };
            outPort = new OutPort() { IsBound = true };
            var channel = new Channel
            {
                OutPort = outPort,
                InPort = inPort
            };
            int newId = channels.Count;
            channels.Add(channel);
            inPort.ChannelHandle = newId;
            outPort.ChannelHandle = newId;
        }

        public void AddChannel(ref OutPort outPort, ref InPort inPort)
        {
            AddChannel(ref inPort, ref outPort);
        }

        public Resource AddResource(int amount)
        {
            int newID = resourceBlockages.Count;
            var resource = new Resource
            {
                Handle = newID,
                Amount = amount
            };
            resourceBlockages.Add(new ResourceBlockage()
            {
                Resource = resource
            });
            return resource;
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

                var fields = actor.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    var value = field.GetValue(actor);
                    string actorName = string.IsNullOrEmpty(actor.Name) ? actor.GetType().Name : actor.Name;
                    Port port;
                    if (field.FieldType == typeof(InPort))
                    {
                        if (value == null)
                        {
                            port = new InPort() { IsBound = false };
                            field.SetValue(actor, port);
                        }
                        else
                        {
                            port = (InPort)value;
                        }
                        port.Name = $"{actorName}.{field.Name}";
                    }
                    else if (field.FieldType == typeof(OutPort))
                    {
                        if (value == null)
                        {
                            port = new OutPort() { IsBound = false };
                            field.SetValue(actor, port);
                        }
                        else
                        {
                            port = (OutPort)value;
                        }
                        port.Name = $"{actorName}.{field.Name}";
                    }
                }
            }

            foreach (var channel in channels)
            {
                channel.Name = $"{channel.OutPort.Name} -> {channel.InPort.Name}";
            }
        }

        public (bool idled, long time, long nrCommands) RunUntil(long stopTime = long.MaxValue, long stopCmds = long.MaxValue)
        {
            long nrCommands = 0;
            long currentTime = 0;
            bool idled = false;
            bool exiting = false;
            while (nrCommands < stopCmds && !exiting)
            {
                if (ready.Count == 0)
                {
                    idled = true;
                    break;
                }

                var currentThread = ready.Dequeue();

                if (currentThread.Time > stopTime)
                    break;

                nrCommands++;
                currentTime = currentThread.Time;
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
                                var aChannel = channels[port.ChannelHandle];
                                if (port.IsBound == false)
                                    continue;
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
                    case ResIncreaseCmd resIncrease:
                        {
                            var blockage = resourceBlockages[resIncrease.Resource.Handle];
                            blockage.Resource.Amount += resIncrease.Amount;
                            CheckBlockage(blockage, currentTime);
                            currentThread.Time = currentTime;
                            ready.Enqueue(currentThread);
                            break;
                        }
                    case ResDecreaseCmd resDecrease:
                        {
                            var blockage = resourceBlockages[resDecrease.Resource.Handle];
                            blockage.Waiting.Add((resDecrease, currentThread));
                            CheckBlockage(blockage, currentTime);
                            break;
                        }
                    case ResCreateCmd resCreate:
                        {
                            var resource = AddResource(resCreate.Initial);
                            resCreate.Resource = resource;
                            ready.Enqueue(currentThread);
                            break;
                        }
                    case ExitCmd exit:
                        {
                            exiting = true;
                            break;
                        }
                    default:
                        throw new Exception("Unknown command: " + cmd);
                }
            }

            return (idled, currentTime, nrCommands);
        }

        private void CheckBlockage(ResourceBlockage blockage, long currentTime)
        {
            // Schedule any of the waiting cmds if enough resources are available
            for (int i = blockage.Waiting.Count - 1; i >= 0; i--)
            {
                var (decreaseCmd, waitingThread) = blockage.Waiting[i];
                if (decreaseCmd.Amount <= blockage.Resource.Amount)
                {
                    blockage.Resource.Amount -= decreaseCmd.Amount;
                    waitingThread.Time = currentTime;
                    ready.Enqueue(waitingThread);
                    blockage.Waiting.RemoveAt(i);
                }
            }
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
                    if (port.IsBound == false)
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

        public void PrintDeadlockReport()
        {
            Console.WriteLine("Waiting messages:");
            foreach (var channel in channels)
            {
                if (channel.SendCmd == null && channel.ReceiveCmd == null)
                {
                    continue;
                }

                if (channel.SendCmd != null)
                {
                    Console.WriteLine($"  Waiting to send \"{channel.SendCmd.Message}\" on \"{channel.Name}\" at time {channel.SendCmd.Time}");

                }
            }
        }
    }

    public abstract class Actor
    {
        protected Environment env;
        public string Name { get; protected set; }

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

    public class ResIncreaseCmd : Command
    {
        // To scheduler
        public Resource Resource;
        public int Amount;
    }

    public class ResDecreaseCmd : Command
    {
        // To scheduler
        public Resource Resource;
        public int Amount;
    }

    public class ResCreateCmd : Command
    {
        // To scheduler
        public int Initial;

        // Result
        public Resource Resource;
    }

    public class ExitCmd : Command
    {

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

        public ResIncreaseCmd Increase(Resource resource, int amount)
        {
            return new ResIncreaseCmd { Resource = resource, Amount = amount };
        }

        public ResDecreaseCmd Decrease(Resource resource, int amount)
        {
            return new ResDecreaseCmd { Resource = resource, Amount = amount };
        }

        public ResCreateCmd CreateResource(int intial)
        {
            return new ResCreateCmd { Initial = intial };
        }

        public ExitCmd Exit()
        {
            return new ExitCmd { };
        }

        public long Now { get; set; }
    }

    public class Port
    {
        public int ChannelHandle;
        public bool IsBound;
        public string Name;

        public override string ToString()
        {
            return Name;
        }
    }

    public class InPort : Port
    {

    }

    public class OutPort : Port
    {

    }
}