using System.Diagnostics;

namespace NewSimulator;

public class Simulator
{
    private List<Actor> actors = new();
    private List<Channel> channels = new();
    private PriorityQueue<Process> ready = new();

    public long Now { get; private set; } = 0;
    public Process? ActiveProcess { get; private set; }
    public int NrEventsProcessed { get; private set; } = 0;

    public Process AddProcess(IEnumerable<Event?> generator)
    {
        var process = new Process(generator.GetEnumerator(), Now);
        ready.Enqueue(process);
        return process;
    }

    public void AddActor(Actor actor)
    {
        AddProcess(actor.Run(this));
        actors.Add(actor);
    }
    
    public void ScheduleD(Process process, long delay)
    {
        process.Time = Now + delay;
        ready.Enqueue(process);
    }

    public void Schedule(Process process)
    {
        process.Time = Now;
        ScheduleD(process, 0);
    }

    public void Run()
    {
        while (ready.Count > 0)
        {
            ActiveProcess = ready.Dequeue();
            NrEventsProcessed++;
            Debug.Assert(ActiveProcess.Time >= Now);
            Now = ActiveProcess.Time;

            Event? yieldedEvent = null;
            bool isDone = false;
            while (yieldedEvent == null && !isDone)
            {
                isDone = !ActiveProcess.Runnable.MoveNext();
                yieldedEvent = ActiveProcess.Runnable.Current;
            }

            if (!isDone)
            {
                yieldedEvent!.Yielded();
            }
            else
            {
                ActiveProcess.OnComplete?.Invoke();
            }
        }
    }

    public Send Send(Channel channel, object message)
    {
        return new Send(this, channel, message);
    }

    public Receive Receive(Channel channel)
    {
        return new Receive(this, channel);
    }

    public Delay Delay(long delay)
    {
        return new Delay(this, delay);
    }

    public WaitProcess Process(IEnumerable<Event> generator)
    {
        var process = AddProcess(generator);
        return new WaitProcess(this, process);
    }
}