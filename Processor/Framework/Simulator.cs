using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SpikingDSE;

public sealed class Simulator
{
    private readonly PriorityQueue<Process> ready = new();
    private readonly List<Channel> channels = new();
    private readonly List<Actor> actors = new();

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
            Time = Now,
            Actor = CurrentProcess.Actor
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


    public T AddActor<T>(T actor) where T : Actor
    {
        actors.Add(actor);
        return actor;
    }

    public void Compile()
    {
        foreach (var actor in actors)
        {
            ready.Enqueue(new Process
            {
                Runnable = actor.Run(this).GetEnumerator(),
                Time = 0,
                Actor = actor
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
                // TODO: Output port array and input port array working
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
            CurrentProcess.Actor.NrEvents++;
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
        if (ev is SleepEvent sleep)
        {
            CurrentProcess.Time += sleep.Time;
            ready.Enqueue(CurrentProcess);
        }
        else if (ev is SendEvent send)
        {
            var channel = channels[send.Port.ChannelHandle];
            if (channel.SendEvent != null)
                throw new Exception("Channel is already occupied");

            channel.SendEvent = send;
            channel.SendProcess = CurrentProcess;
            if (channel.SendEvent != null && channel.ReceiveEvent != null)
                DoChannelTransfer(channel);
        }
        else if (ev is ReceiveEvent recv)
        {
            var channel = channels[recv.Port.ChannelHandle];
            if (channel.ReceiveEvent != null)
                throw new Exception("Channel is already occupied");

            channel.ReceiveEvent = recv;
            channel.ReceiveProcess = CurrentProcess;
            if (channel.SendEvent != null && channel.ReceiveEvent != null)
                DoChannelTransfer(channel);
        }
        else if (ev is MutexReqEvent resWait)
        {
            var res = resWait.Mutex;
            res.Waiting.Add(resWait);
            CheckBlocking(res);
        }
        else if (ev is ProcessWaitEvent processWait)
        {
            processWait.Process.Waiting.Add(CurrentProcess);
        }
        else if (ev is SignalWaitEvent signalWait)
        {
            signalWait.Signal.Waiting.Add(signalWait);
        }
        else
        {
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
        var rcv = channel.ReceiveEvent;
        rcv.StartedReceiving = Now;
        var snd = channel.SendEvent;
        rcv.Message = channel.SendEvent.Message;

        // Queue threads
        if (rcv.TransferTime > 0 && snd.TransferTime > 0)
            throw new Exception("Transfer time can only be configured from one side");
        long newTime = Math.Max(snd.Time, rcv.Time) + Math.Max(rcv.TransferTime, snd.TransferTime);

        // Send is always acknowledged
        channel.ReceiveProcess.Time = newTime;
        ready.Enqueue(channel.ReceiveProcess);
        channel.ReceiveEvent = null;
        channel.ReceiveProcess = null;

        if (rcv.Ack)
        {
            channel.SendProcess.Time = newTime;
            ready.Enqueue(channel.SendProcess);
            channel.SendEvent = null;
            channel.SendProcess = null;
        }
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

    public void PrintActorReport()
    {
        Console.WriteLine("Actor # events handled:");
        foreach (var actor in actors)
        {
            Console.WriteLine($"  {actor.Name}: {actor.NrEvents:n}");
        }
    }

    public void RcvAck(InPort port)
    {
        if (!port.IsBound)
            throw new Exception("Port is not bound!");

        var channel = channels[port.ChannelHandle];
        channel.SendProcess.Time = Now;
        ready.Enqueue(channel.SendProcess);
        channel.SendEvent = null;
        channel.SendProcess = null;
    }

    public SleepEvent Delay(long delay)
    {
        long newTime = Now + delay;
        if (newTime < Now)
            throw new Exception("Can not go back in time");

        return new SleepEvent { Time = delay };
    }

    public SleepEvent SleepUntil(long newTime)
    {
        if (newTime < Now)
            throw new Exception("Can not go back in time");

        return new SleepEvent { Time = newTime - Now };
    }

    public SendEvent Send(OutPort port, object message, int transferTime = 0)
    {
        if (!port.IsBound)
            throw new Exception("Port is not bound!");

        return new SendEvent { Port = port, Message = message, Time = Now, TransferTime = transferTime };
    }

    public SendEvent SendAt(OutPort port, object message, long time)
    {
        if (time < Now)
            throw new Exception("Can not go back in time!");

        if (!port.IsBound)
            throw new Exception("Port is not bound!");

        return new SendEvent { Port = port, Message = message, Time = time };
    }

    public ReceiveEvent Receive(InPort port, long transferTime = 0, bool ack = true)
    {
        if (!port.IsBound)
            throw new Exception("Port is not bound!");

        return new ReceiveEvent { Port = port, Time = Now, TransferTime = transferTime, Ack = ack };
    }

    public ProcessWaitEvent Process(IEnumerable<Event> runnable)
    {
        var process = AddProcess(runnable);
        return new ProcessWaitEvent { Process = process };
    }
}