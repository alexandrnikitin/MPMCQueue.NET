using System;
using System.Threading;
using Xunit;

namespace MPMCQueue.NET.Tests
{
    public class MultiThreadedTests
    {
        private Message _msg = new Message() { IsWorking = true };
        private Message _stopMsg = new Message() { IsWorking = false };

        private const int Operations = 1 << 25;
        private const int NumberOfThreads = 2;

        private readonly int _bufferSize = 1 << 25;
        private readonly ManualResetEventSlim _reset = new ManualResetEventSlim(false);

        private readonly MPMCQueue _queue;
        private readonly Thread[] _threads;
        private readonly Thread[] _threadsConsumers;

        private bool isFailed = false;

        public MultiThreadedTests()
        {
            _queue = new MPMCQueue(_bufferSize);
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
                        object ret;
                        if (_queue.TryDequeue(out ret))
                        {
                            isWorking = ((Message)ret).IsWorking;
                        }
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
                        if (!_queue.TryEnqueue(_msg))
                        {
                            isFailed = true;
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
                _queue.TryEnqueue(_stopMsg);
            }

            for (var i = 0; i < _threadsConsumers.Length; i++)
            {
                _threadsConsumers[i].Join();
            }

            Assert.False(isFailed);
        }
    }
}