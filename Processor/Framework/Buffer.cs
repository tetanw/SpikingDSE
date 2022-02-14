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
        this.itemsFilled = new Mutex(env, 0);
        this.itemsEmpty = new Mutex(env, size);
    }

    public MutexReqEvent RequestRead()
    {
        return itemsFilled.Wait(1);
    }

    public T Read()
    {
        return items.Dequeue();
    }

    public void ReleaseRead()
    {
        env.Increase(itemsEmpty, 1);
    }

    public MutexReqEvent RequestWrite()
    {
        return itemsEmpty.Wait(1);
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
        itemsEmpty.Decrease(1);
        env.Increase(itemsFilled, 1);
    }

    public T Pop()
    {
        T item = items.Dequeue();
        itemsFilled.Decrease(1);
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
