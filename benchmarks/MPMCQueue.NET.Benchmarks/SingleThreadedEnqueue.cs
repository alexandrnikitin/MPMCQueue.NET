using System;
using BenchmarkDotNet.Attributes;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(SingleRunConfig))]
    public class SingleThreadedEnqueue
    {
        private const int Operations = 1 << 23;
        MPMCQueue<int> _queue;
        private readonly int _bufferSize = Operations;

        [Setup]
        public void Setup()
        {
            _queue = new MPMCQueue<int>(_bufferSize);
        }

        [Benchmark(OperationsPerInvoke = Operations)]
        public void Enqueue()
        {
            for (var i = 0; i < Operations; i++)
            {
                _queue.TryEnqueue(1);
            }
        }
    }
}