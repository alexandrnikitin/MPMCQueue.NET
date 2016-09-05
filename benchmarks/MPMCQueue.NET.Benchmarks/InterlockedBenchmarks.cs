using System.Threading;
using BenchmarkDotNet.Attributes;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(Config))]
    public class InterlockedBenchmarks
    {
        private int _counter;

        [Benchmark]
        public void Increment()
        {
            Interlocked.Increment(ref _counter);
        }

        [Benchmark]
        public void CAS()
        {
            Interlocked.CompareExchange(ref _counter, _counter + 1, _counter);
        }
    }

}