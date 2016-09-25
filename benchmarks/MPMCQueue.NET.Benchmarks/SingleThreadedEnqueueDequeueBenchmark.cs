using BenchmarkDotNet.Attributes;
using MPMCQueue.NET.Benchmarks.Configs;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(Config))]
    public class SingleThreadedEnqueueDequeueBenchmark
    {
        private readonly object _obj = new object();
        MPMCQueue _queue;
        private readonly int _bufferSize = 65536;

        [Setup]
        public void Setup()
        {
            _queue = new MPMCQueue(_bufferSize);
        }

        [Benchmark]
        public void EnqueueDequeue()
        {
            _queue.TryEnqueue(_obj);

            object ret;
            _queue.TryDequeue(out ret);
        }
    }
}