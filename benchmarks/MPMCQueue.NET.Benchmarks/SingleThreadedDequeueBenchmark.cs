using BenchmarkDotNet.Attributes;
using MPMCQueue.NET.Benchmarks.Configs;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(SingleRunConfig))]
    public class SingleThreadedDequeueBenchmark
    {
        private readonly object _obj = new object();
        private const int Operations = 1 << 23;
        MPMCQueue _queue;
        private readonly int _bufferSize = Operations;

        [Setup]
        public void Setup()
        {
            _queue = new MPMCQueue(_bufferSize);
            for (var i = 0; i < Operations; i++)
            {
                _queue.TryEnqueue(_obj);
            }
        }

        [Benchmark(OperationsPerInvoke = Operations)]
        public void Dequeue()
        {
            for (var i = 0; i < Operations; i++)
            {
                object item;
                _queue.TryDequeue(out item);
            }
        }
    }
}