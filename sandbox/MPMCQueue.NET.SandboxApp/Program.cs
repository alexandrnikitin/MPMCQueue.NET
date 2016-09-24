using System;
using System.Diagnostics;

namespace MPMCQueue.NET.SandboxApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var sut = new MultiThreadedMPMCQueueBenchmark();
            sut.NumberOfThreads = 4;
            sut.Setup();
            var sw = Stopwatch.StartNew();

            sut.EnqueueDequeue();
            sw.Stop();

            Console.WriteLine(sw.ElapsedMilliseconds);
            Console.WriteLine(MultiThreadedMPMCQueueBenchmark.Operations);
        }
    }
}
