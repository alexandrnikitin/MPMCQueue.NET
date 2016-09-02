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
            int actual;
            _queue.TryDequeue(out actual);

            Assert.Equal(1, actual);
        }
    }
}