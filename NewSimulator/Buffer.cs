namespace NewSimulator;

public sealed class WriteRequest : Event, IDisposable
{
    public WriteRequest(Simulator sim) : base(sim)
    {
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void Trigger()
    {
        Sim.Schedule(Process);
    }

    public override void Yielded()
    {

    }
}

public sealed class ReadRequest : Event, IDisposable
{
    public ReadRequest(Simulator sim) : base(sim)
    {
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void Trigger()
    {
        Sim.Schedule(Process);
    }

    public override void Yielded()
    {

    }
}

public class Buffer<T>
{
    private Simulator env;
    private Queue<ReadRequest> waitingReads = new();
    private Queue<WriteRequest> waitingWrites = new();
    public int Size { get; set; }
    public int ItemsFilled { get; set; }
    public int ItemsEmpty { get; set; }
    private Queue<T> items = new();

    public Buffer(Simulator env, int size)
    {
        this.env = env;
        this.Size = size;
        this.ItemsFilled = 0;
        this.ItemsEmpty = size;
    }

    public WriteRequest RequestWrite()
    {
        if (IsFull)
        {

        }
        else
        {

        }
    }

    public ReadRequest RequestRead()
    {

    }

    public void Write(T item)
    {
        if (IsFull)
            throw new Exception("Buffer is already full");

        items.Enqueue(item);
        PollWaitingReads();
    }

    public T Read()
    {
        if (IsEmpty)
            throw new Exception("Buffer is empty");

        var item = items.Dequeue();
        PollWaitingWrites();
        return item;
    }

    private void PollWaitingReads()
    {
        if (waitingReads.Count == 0)
            return;
        var reader = waitingReads.Dequeue();
        reader.Trigger();
    }

    private void PollWaitingWrites()
    {
        if (waitingWrites.Count == 0)
            return;
        var writer = waitingWrites.Dequeue();
        writer.Trigger();
    }

    public int Count { get => items.Count; }
    public bool IsEmpty { get => ItemsEmpty == Size; }
    public bool IsFull { get => ItemsFilled == Size; }
}