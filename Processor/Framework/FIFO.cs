using System.Collections.Generic;

namespace SpikingDSE
{
    public sealed class FIFO<T>
    {
        private Environment env;
        private Queue<T> items;
        private int size;
        private Resource itemsFilled;
        private Resource itemsEmpty;

        public FIFO(Environment env, int size)
        {
            this.env = env;
            this.items = new Queue<T>(size);
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
            env.IncreaseResource(itemsEmpty, 1);
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
            env.IncreaseResource(itemsFilled, 1);
        }

        public void Push(T item)
        {
            items.Enqueue(item);
            env.DecreaseResource(itemsEmpty, 1);
            env.IncreaseResource(itemsFilled, 1);
        }

        public T Pop()
        {
            T item = items.Dequeue();
            env.DecreaseResource(itemsFilled, 1);
            env.IncreaseResource(itemsEmpty, 1);
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
    }
}