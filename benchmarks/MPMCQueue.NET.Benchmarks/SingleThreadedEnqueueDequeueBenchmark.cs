using BenchmarkDotNet.Attributes;
using MPMCQueue.NET.Benchmarks.Configs;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(Config))]
    public class SingleThreadedEnqueueDequeueBenchmark
    {
        MPMCQueue<int> _queue;
        private readonly int _bufferSize = 65536;

        [Setup]
        public void Setup()
        {
            _queue = new MPMCQueue<int>(_bufferSize);
        }

        [Benchmark]
        public void EnqueueDequeue()
        {
            _queue.TryEnqueue(1);

            int msg;
            _queue.TryDequeue(out msg);
        }
    }
}