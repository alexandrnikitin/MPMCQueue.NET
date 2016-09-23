using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using MPMCQueue.NET.Benchmarks.Configs;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(SingleRunConfig))]
    public class MultiThreadedMPMCQueueSandboxBenchmark
    {
        private const int Operations = 1 << 25;

        [Params(4)]
        public int NumberOfThreads { get; set; }

        private readonly int _bufferSize = 1 << 25;
        private readonly ManualResetEventSlim _reset = new ManualResetEventSlim(false);

        private Sandbox.V4.MPMCQueue<bool> _queue;
        private Thread[] _threads;
        private Thread[] _threadsConsumers;

        [Setup]
        public void Setup()
        {
            _queue = new Sandbox.V4.MPMCQueue<bool>(_bufferSize);
            _threadsConsumers = LaunchConsumers(NumberOfThreads);
            _threads = LaunchProducers(Operations, NumberOfThreads);
        }

        private Thread[] LaunchConsumers(int numberOfThreads)
        {
            var threads = new Thread[numberOfThreads];
            for (var i = 0; i < numberOfThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    var isWorking = true;
                    while (isWorking)
                    {
                        _queue.TryDequeue(out isWorking);
                    }
                });
                thread.Start();
                threads[i] = thread;
            }

            return threads;
        }

        private Thread[] LaunchProducers(int numberOfOperations, int numberOfThreads)
        {
            var threads = new Thread[numberOfThreads];
            var opsPerThread = numberOfOperations / numberOfThreads;
            for (var i = 0; i < numberOfThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    _reset.Wait();
                    for (var j = 0; j < opsPerThread; j++)
                    {
                        if (!_queue.TryEnqueue(true))
                        {
                            throw new Exception();
                        }
                    }
                });
                thread.Start();
                threads[i] = thread;
            }
            return threads;
        }

        [Benchmark(OperationsPerInvoke = Operations)]
        public void EnqueueDequeue()
        {
            _reset.Set();
            for (var i = 0; i < _threads.Length; i++)
            {
                _threads[i].Join();
            }

            for (var i = 0; i < NumberOfThreads * 8; i++)
            {
                _queue.TryEnqueue(false);
            }

            for (var i = 0; i < _threadsConsumers.Length; i++)
            {
                _threadsConsumers[i].Join();
            }

        }
    }
}