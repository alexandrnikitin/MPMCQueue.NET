using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using MPMCQueue.NET.Benchmarks.Configs;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(Config))]
    public class ConcurrentQueueBenchmarks
    {
        ConcurrentQueue<int> _queue;

        [Setup]
        public void Setup()
        {
            _queue = new ConcurrentQueue<int>();
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