using BenchmarkDotNet.Running;

namespace MPMCQueue.NET.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MultiThreadedMPMCQueueV2>();
        }
    }
}