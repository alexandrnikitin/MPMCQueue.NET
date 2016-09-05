using System;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace MPMCQueue.NET.Benchmarks
{
    [Config(typeof(SingleRunConfig))]
    public class MultiThreadedMPMCQueueSandbox
    {
        private const int Operations = 1 << 25;
        private const int NumberOfThreads = 2;

        private readonly int _bufferSize = 1 << 25;
        private readonly ManualResetEventSlim _reset = new ManualResetEventSlim(false);

        private MPMCQueue.NET.Sandbox.V2.MPMCQueue<bool> _queue;
        private Thread[] _threads;

        [Setup]
        public void Setup()
        {
            _queue = new MPMCQueue.NET.Sandbox.V2.MPMCQueue<bool>(_bufferSize);
            LaunchConsumers(NumberOfThreads);
            _threads = LaunchProducers(Operations, NumberOfThreads);
        }

        private void LaunchConsumers(int numberOfThreads)
        {
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
            }
        }

        private Thread[] LaunchProducers(int numberOfOperations, int numberOfThreads)
        {
            var threads = new Thread[numberOfThreads];
            var opsPerThread = numberOfOperations/numberOfThreads;
            for (var i = 0; i < numberOfThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    _reset.Wait();
                    for (var j = 0; j < opsPerThread; j++)
                    {
                        _queue.TryEnqueue(true);
                    }
                });
                thread.Start();
                threads[i] = thread;
            }
            return threads;
        }

        [Benchmark(OperationsPerInvoke = Operations/NumberOfThreads)]
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
        }
    }
}