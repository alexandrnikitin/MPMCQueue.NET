using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MPMCQueue.NET.Tests.FieldReports
{
    public class Issue10_InfiniteLoopWhenOverflowAndEmpty
    {
        private readonly MPMCQueue _queue = new MPMCQueue(16);

        [Fact(Skip = "Long running")]
        public void Test()
        {
            var elem = new object();
            for (int i = 0; i < 2147483632; i++)
            {
                _queue.TryEnqueue(elem);
                _queue.TryDequeue(out elem);
            }

            for (int i = 0; i < 16; i++)
            {
                _queue.TryEnqueue(elem);
            }
            // the next call should not loop infinitely
            var task = Task.Run(() => _queue.TryEnqueue(elem));
            Thread.Sleep(100);
            Assert.True(task.IsCompleted);
        }
    }
}