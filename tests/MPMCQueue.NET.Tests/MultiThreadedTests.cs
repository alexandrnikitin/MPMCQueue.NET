using System;
using System.Threading;
using Xunit;

namespace MPMCQueue.NET.Tests
{
    public class MultiThreadedTests
    {
        private const int Operations = 1 << 25;
        private const int NumberOfThreads = 2;

        private readonly int _bufferSize = 1 << 25;
        private readonly ManualResetEventSlim _reset = new ManualResetEventSlim(false);

        private readonly MPMCQueue<bool> _queue;
        private readonly Thread[] _threads;
        private readonly Thread[] _threadsConsumers;

        public MultiThreadedTests()
        {
            _queue = new MPMCQueue<bool>(_bufferSize);
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

        [Fact]
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