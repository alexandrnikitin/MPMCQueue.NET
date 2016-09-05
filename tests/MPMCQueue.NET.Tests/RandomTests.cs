using Xunit;

namespace MPMCQueue.NET.Tests
{
    public class RandomTests
    {
        private readonly MPMCQueue<int> _queue;

        public RandomTests()
        {
            _queue = new MPMCQueue<int>(65536);
        }

        [Fact]
        public void CanEnqueueAndDequeue()
        {
            _queue.TryEnqueue(1);
            _queue.TryEnqueue(2);
            _queue.TryEnqueue(3);
            int actual;
            _queue.TryDequeue(out actual);
            Assert.Equal(1, actual);

            _queue.TryDequeue(out actual);
            Assert.Equal(2, actual);

            _queue.TryDequeue(out actual);
            Assert.Equal(3, actual);
        }
    }
}