using System.Collections.Generic;

namespace SpikingDSE;
public sealed class FIFO<T>
{
    private Simulator env;
    private Queue<T> items;
    private int size;
    private Resource itemsFilled;
    private Resource itemsEmpty;

    public FIFO(Simulator env, int size)
    {
        this.env = env;
        this.items = new Queue<T>();
        this.size = size;
        this.itemsFilled = env.CreateResource(0);
        this.itemsEmpty = env.CreateResource(size);
    }

    public ResReqEvent RequestRead()
    {
        return env.RequestResource(itemsFilled, 1);
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
        return env.RequestResource(itemsEmpty, 1);
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
