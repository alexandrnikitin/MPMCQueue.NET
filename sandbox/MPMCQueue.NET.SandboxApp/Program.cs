using System;
using System.Diagnostics;
using MPMCQueue.NET.Benchmarks;

namespace MPMCQueue.NET.SandboxApp
{
    class Program
    {
        static void Main(string[] args)
        {
            for (int i = 0; i < 1000; i++)
            {
                var sut = new MultiThreadedMPMCQueueBenchmark();
                sut.NumberOfThreads = 2;
                sut.Setup();
                var sw = Stopwatch.StartNew();

                sut.EnqueueDequeue();
                sw.Stop();

                Console.WriteLine(sw.ElapsedMilliseconds);

                //Console.ReadKey();

            }
            Console.WriteLine(MultiThreadedMPMCQueueBenchmark.Operations);
        }
    }
}
