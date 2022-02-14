using System.Collections.Generic;

namespace SpikingDSE;
public sealed class Buffer<T>
{
    private Simulator env;
    private Queue<T> items;
    private int size;
    private Mutex itemsFilled;
    private Mutex itemsEmpty;

    public Buffer(Simulator env, int size)
    {
        this.env = env;
        this.items = new Queue<T>();
        this.size = size;
        this.itemsFilled = new Mutex(0);
        this.itemsEmpty = new Mutex(size);
    }

    public ResReqEvent RequestRead()
    {
        return env.Wait(itemsFilled, 1);
    }

    public T Read()
    {
        return items.Dequeue();
    }

    public void ReleaseRead()
    {
        env.Increase(itemsEmpty, 1);
    }

    public ResReqEvent RequestWrite()
    {
        return env.Wait(itemsEmpty, 1);
    }

    public void Write(T item)
    {
        items.Enqueue(item);
    }

    public void ReleaseWrite()
    {
        env.Increase(itemsFilled, 1);
    }

    public void Push(T item)
    {
        items.Enqueue(item);
        env.Decrease(itemsEmpty, 1);
        env.Increase(itemsFilled, 1);
    }

    public T Pop()
    {
        T item = items.Dequeue();
        env.Decrease(itemsFilled, 1);
        env.Increase(itemsEmpty, 1);
        return item;
    }

    public T Peek()
    {
        return items.Peek();
    }

    public int Count
    {
        get => items.Count;
    }

    public bool IsFull
    {
        get => itemsEmpty.Amount == 0;
    }

    public bool IsEmpty
    {
        get => itemsEmpty.Amount == size;
    }
}
