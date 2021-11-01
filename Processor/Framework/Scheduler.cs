using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SpikingDSE
{
    public sealed class Process : IComparable<Process>
    {
        public IEnumerator<Event> Runnable;
        public long Time;
        public List<Process> Waiting;

        public int CompareTo([AllowNull] Process other)
        {
            return Time.CompareTo(other.Time);
        }
    }

    public sealed class Channel
    {
        public string Name;
        public OutPort OutPort;
        public InPort InPort;

        public Process SendProcess;
        public SendEvent SendEvent;
        public Process ReceiveProcess;
        public Event ReceiveEvent;

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class Resource
    {
        public List<(ResReqEvent, Process)> Waiting;
        public int Amount;
    }

    public sealed class Signal
    {
        public List<Process> Waiting;
    }

    public sealed class Scheduler
    {
        private PriorityQueue<Process> ready = new PriorityQueue<Process>();
        private List<Channel> channels = new List<Channel>();
        private List<Actor> actors = new List<Actor>();
        private Environment env;

        private long nrEvents;
        private Process currentThread;
        private long currentTime;

        public Scheduler()
        {
            this.env = new Environment(this);
        }

        public Signal CreateSignal()
        {
            var signal = new Signal();
            return signal;
        }

        public void Notify(Signal signal)
        {
            foreach (var thread in signal.Waiting)
            {
                thread.Time = currentTime;
                ready.Enqueue(thread);
            }
            signal.Waiting.Clear();
        }

        public void Increase(Resource resource, int amount)
        {
            resource.Amount += amount;
            CheckBlocking(resource);
        }

        public void Decrease(Resource resource, int amount)
        {
            resource.Amount -= amount;
        }

        public Resource CreateResource(int initial)
        {
            var resource = new Resource
            {
                Amount = initial
            };
            return resource;
        }

        public Process AddProcess(IEnumerable<Event> runnable)
        {
            var process = new Process
            {
                Runnable = runnable.GetEnumerator(),
                Time = currentTime
            };
            ready.Enqueue(process);
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

        public T AddActor<T>(T process) where T : Actor
        {
            actors.Add(process);
            return process;
        }

        public void AddChannel(ref OutPort outPort, ref InPort inPort)
        {
            AddChannel(ref inPort, ref outPort);
        }

        public void Compile()
        {
            foreach (var actor in actors)
            {
                ready.Enqueue(new Process
                {
                    Runnable = actor.Run(env).GetEnumerator(),
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

        public (long time, long nrEvents) RunUntil(long stopTime = long.MaxValue, long stopEvents = long.MaxValue)
        {
            currentTime = 0;
            while (nrEvents < stopEvents && currentTime < stopTime && ready.Count > 0)
            {
                currentThread = ready.Dequeue();
                nrEvents++;

                currentTime = currentThread.Time;
                env.Now = currentThread.Time;
                env.CurrentThread = currentThread;

                bool workAvail = currentThread.Runnable.MoveNext();
                if (!workAvail)
                {
                    OnThreadCompleted(currentThread);
                    continue;
                }

                var ev = currentThread.Runnable.Current;
                HandleEvent(ev);
            }

            return (currentTime, nrEvents);
        }

        private void HandleEvent(Event ev)
        {
            switch (ev)
            {
                case SleepEvent sleep:
                    {
                        currentThread.Time += sleep.Time;
                        ready.Enqueue(currentThread);
                        break;
                    }
                case SendEvent send:
                    {
                        var channel = channels[send.Port.ChannelHandle];
                        if (channel.SendEvent != null)
                        {
                            throw new Exception("Channel is already occupied");
                        }

                        channel.SendEvent = send;
                        channel.SendProcess = currentThread;
                        if (channel.ReceiveEvent != null)
                        {
                            DoChannelTransfer(channel);
                        }
                        break;
                    }
                case ReceiveEvent recv:
                    {
                        var channel = channels[recv.Port.ChannelHandle];
                        if (channel.ReceiveEvent != null)
                        {
                            throw new Exception("Channel is already occupied");
                        }

                        channel.ReceiveEvent = recv;
                        channel.ReceiveProcess = currentThread;
                        if (channel.SendEvent != null)
                        {
                            DoChannelTransfer(channel);
                        }
                        break;
                    }
                case SelectEvent select:
                    {
                        for (int i = 0; i < select.Ports.Length; i++)
                        {
                            var port = select.Ports[i];
                            var channel = channels[port.ChannelHandle];
                            if (port.IsBound == false)
                                continue;
                            channel.ReceiveEvent = select;
                            channel.ReceiveProcess = currentThread;
                            if (channel.SendEvent != null)
                            {
                                DoChannelTransfer(channel);
                                break;
                            }
                        }

                        break;
                    }
                case ResReqEvent resWait:
                    {
                        var res = resWait.Resource;
                        if (res.Waiting == null)
                            res.Waiting = new List<(ResReqEvent, Process)>();
                        res.Waiting.Add((resWait, currentThread));
                        CheckBlocking(res);
                        break;
                    }
                case ProcessWaitEvent processWait:
                    {
                        if (processWait.Process.Waiting == null)
                            processWait.Process.Waiting = new List<Process>();
                        processWait.Process.Waiting.Add(currentThread);
                        break;
                    }
                case SignalWaitEvent signalWait:
                    {
                        if (signalWait.Signal.Waiting == null)
                            signalWait.Signal.Waiting = new List<Process>();
                        signalWait.Signal.Waiting.Add(currentThread);
                        break;
                    }
                default:
                    throw new Exception("Unknown event: " + ev);
            }
        }

        private void OnThreadCompleted(Process thread)
        {
            if (thread.Waiting == null) return;
            foreach (var waitingThread in thread.Waiting)
            {
                waitingThread.Time = currentTime;
                ready.Enqueue(waitingThread);
            }
            thread.Waiting.Clear();
        }

        private void CheckBlocking(Resource resource)
        {
            // Schedule any of the waiting cmds if enough resources are available
            if (resource.Waiting == null) return;
            for (int i = resource.Waiting.Count - 1; i >= 0; i--)
            {
                var (decreaseCmd, waitingThread) = resource.Waiting[i];
                if (decreaseCmd.Amount <= resource.Amount)
                {
                    resource.Amount -= decreaseCmd.Amount;
                    waitingThread.Time = currentTime;
                    ready.Enqueue(waitingThread);
                    resource.Waiting.RemoveAt(i);
                }
            }
        }

        private void DoChannelTransfer(Channel channel)
        {
            if (channel.ReceiveEvent is ReceiveEvent)
            {
                var rcv = channel.ReceiveEvent as ReceiveEvent;
                rcv.Message = channel.SendEvent.Message;
                long newTime = Math.Max(channel.SendEvent.Time, rcv.Time);
                QueueThreads(channel, newTime);
                CleanChannel(channel);
            }
            else if (channel.ReceiveEvent is SelectEvent)
            {
                var select = channel.ReceiveEvent as SelectEvent;
                select.Message = channel.SendEvent.Message;
                select.Port = channel.InPort;
                long newTime = Math.Max(channel.SendEvent.Time, select.Time);
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
            channel.SendEvent = null;
            channel.SendProcess = null;
            channel.ReceiveEvent = null;
            channel.ReceiveProcess = null;
        }

        private void QueueThreads(Channel channel, long newTime)
        {
            channel.SendProcess.Time = newTime;
            channel.ReceiveProcess.Time = newTime;
            ready.Enqueue(channel.SendProcess);
            ready.Enqueue(channel.ReceiveProcess);
        }

        public void PrintDeadlockReport()
        {
            Console.WriteLine("Waiting messages:");
            foreach (var channel in channels)
            {
                if (channel.SendEvent == null && channel.ReceiveEvent == null)
                {
                    continue;
                }

                if (channel.SendEvent != null)
                {
                    Console.WriteLine($"  Waiting to send \"{channel.SendEvent.Message}\" on \"{channel.Name}\" at time {channel.SendEvent.Time}");
                }
            }
        }
    }
}