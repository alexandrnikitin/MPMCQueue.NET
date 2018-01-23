using System;
using System.Threading.Tasks;
using Xunit;

namespace MPMCQueue.NET.Tests.FieldReports
{
    public class Issue2_TryDequeueTrueButGetNull
    {
        static MPMCQueue queue { get; } = new MPMCQueue(2);
        static void Enqueue()
        {
            while (true) { queue.TryEnqueue(1); }
        }

        static void Dequeue()
        {
            while (true)
            {
                if (queue.TryDequeue(out object t) && t == null)
                {
                    throw new Exception("Dequeue null");
                }
            }
        }

        [Fact]
        public void Test()
        {
            var t1 = Task.Run(() => Enqueue());
            var t2 = Task.Run(() => Dequeue());
            t2.Wait(TimeSpan.FromSeconds(10));
        }
    }
}