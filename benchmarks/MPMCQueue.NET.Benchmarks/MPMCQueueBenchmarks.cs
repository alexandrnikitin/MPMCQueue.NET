using BenchmarkDotNet.Attributes;
using MPMCQueue.NET;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(Config))]
    public class MPMCQueueBenchmarks
    {
        MPMCQueue<int> _queue;

        [Setup]
        public void Setup()
        {
            _queue = new MPMCQueue<int>();
        }

        [Benchmark]
        public void EnqueueDequeue()
        {
            _queue.Enqueue(1);

            int msg;
            _queue.TryDequeue(out msg);
        }
    }
}