namespace MPMCQueue.NET
{
    public class MPMCQueue<T>
    {
        public void Enqueue(T item)
        {
        }

        public bool TryDequeue(out T result)
        {
            result = default(T);
            return true;
        }
    }
}