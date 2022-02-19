using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SpikingDSE;

public sealed class Simulator
{
    private PriorityQueue<Process> ready = new PriorityQueue<Process>();
    private List<Channel> channels = new List<Channel>();
    private List<Actor> actors = new List<Actor>();

    public long NrEventsProcessed { get; private set; }
    public Process CurrentProcess { get; private set; }
    public long Now { get; private set; }

    public void Schedule(Process process)
    {
        process.Time = Now;
        ready.Enqueue(process);
    }

    public void Increase(Mutex mutex, int amount)
    {
        mutex.Amount += amount;
        CheckBlocking(mutex);
    }

    public Process AddProcess(IEnumerable<Event> runnable)
    {
        var process = new Process
        {
            Runnable = runnable.GetEnumerator(),
            Time = Now
        };
        ready.Enqueue(process);
        return process;
    }

    public void AddChannel(InPort inPort, OutPort outPort)
    {
        if (inPort.IsBound || outPort.IsBound)
        {
            throw new Exception("Port already bound");
        }

        inPort.IsBound = true;
        outPort.IsBound = true;
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

    public void AddChannel(OutPort outPort, InPort inPort) => AddChannel(inPort, outPort);


    public T AddActor<T>(T process) where T : Actor
    {
        actors.Add(process);
        return process;
    }

    public void Compile()
    {
        foreach (var actor in actors)
        {
            ready.Enqueue(new Process
            {
                Runnable = actor.Run(this).GetEnumerator(),
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

    public void RunUntil(long stopTime = long.MaxValue, long stopEvents = long.MaxValue)
    {
        Now = 0;
        while (NrEventsProcessed < stopEvents && Now < stopTime && ready.Count > 0)
        {
            CurrentProcess = ready.Dequeue();
            Now = CurrentProcess.Time;
            NrEventsProcessed++;

            bool workAvail = CurrentProcess.Runnable.MoveNext();
            if (!workAvail)
            {
                OnThreadCompleted(CurrentProcess);
                continue;
            }

            var ev = CurrentProcess.Runnable.Current;
            HandleEvent(ev);
        }
    }

    private void HandleEvent(Event ev)
    {
        switch (ev)
        {
            case SleepEvent sleep:
                {
                    CurrentProcess.Time += sleep.Time;
                    ready.Enqueue(CurrentProcess);
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
                    channel.SendProcess = CurrentProcess;
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
                    channel.ReceiveProcess = CurrentProcess;
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
                        channel.ReceiveProcess = CurrentProcess;
                        if (channel.SendEvent != null)
                        {
                            DoChannelTransfer(channel);
                            break;
                        }
                    }

                    break;
                }
            case MutexReqEvent resWait:
                {
                    var res = resWait.Mutex;
                    res.Waiting.Add(resWait);
                    CheckBlocking(res);
                    break;
                }
            case ProcessWaitEvent processWait:
                {
                    processWait.Process.Waiting.Add(CurrentProcess);
                    break;
                }
            case SignalWaitEvent signalWait:
                {
                    signalWait.Signal.Waiting.Add(signalWait);
                    break;
                }
            default:
                throw new Exception("Unknown event: " + ev);
        }
    }

    private void OnThreadCompleted(Process thread)
    {
        foreach (var waitingThread in thread.Waiting)
        {
            waitingThread.Time = Now;
            ready.Enqueue(waitingThread);
        }
        thread.Waiting.Clear();
    }

    private void CheckBlocking(Mutex mutex)
    {
        // Schedule any of the waiting cmds if enough mutexs are available
        for (int i = mutex.Waiting.Count - 1; i >= 0; i--)
        {
            var decreaseCmd = mutex.Waiting[i];
            if (decreaseCmd.Amount <= mutex.Amount)
            {
                var proc = decreaseCmd.Process;
                mutex.Amount -= decreaseCmd.Amount;
                proc.Time = Now;
                ready.Enqueue(proc);
                mutex.Waiting.RemoveAt(i);
            }
        }
    }

    private void DoChannelTransfer(Channel channel)
    {
        if (channel.ReceiveEvent is ReceiveEvent)
        {
            var rcv = channel.ReceiveEvent as ReceiveEvent;
            rcv.Message = channel.SendEvent.Message;
            long newTime = Math.Max(channel.SendEvent.Time, rcv.Time) + rcv.TransferTime;
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
        channel.ReceiveProcess.Time = newTime;
        ready.Enqueue(channel.ReceiveProcess);
        channel.SendProcess.Time = newTime;
        ready.Enqueue(channel.SendProcess);
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

    public SleepEvent Delay(long time)
    {
        if (time < 0)
            throw new Exception("Can not go back in time");

        return new SleepEvent { Time = time };
    }

    public SleepEvent SleepUntil(long newTime)
    {
        if (newTime < Now)
            throw new Exception("Can not go back in time");

        return new SleepEvent { Time = newTime - Now };
    }

    public SendEvent Send(OutPort port, object message)
    {
        if (!port.IsBound)
        {
            throw new Exception("Port is not bound!");
        }
        return new SendEvent { Port = port, Message = message, Time = Now };
    }

    public SendEvent SendAt(OutPort port, object message, long time)
    {
        if (!port.IsBound)
        {
            throw new Exception("Port is not bound!");
        }
        return new SendEvent { Port = port, Message = message, Time = time };
    }

    public ReceiveEvent Receive(InPort port, long transferTime = 0)
    {
        if (!port.IsBound)
        {
            throw new Exception("Port is not bound!");
        }
        return new ReceiveEvent { Port = port, Time = Now, TransferTime = transferTime };
    }

    public SelectEvent Select(params InPort[] ports)
    {
        return new SelectEvent { Ports = ports, Time = Now };
    }

    public ProcessWaitEvent Process(IEnumerable<Event> runnable)
    {
        var process = AddProcess(runnable);
        return new ProcessWaitEvent { Process = process };
    }
}