using System;
using System.Diagnostics;
using System.Threading;

namespace MPMCQueue.NET.SandboxApp
{
    public class Message
    {
        public bool IsWorking { get; set; }
    }

    public class MultiThreadedMPMCQueueBenchmark
    {
        private Message _msg = new Message() {IsWorking = true};
        private Message _stopMsg = new Message() {IsWorking = false};
        public const int Operations = 1 << 26;

        public int NumberOfThreads { get; set; }

        private readonly int _bufferSize = 1 << 24;
        private readonly ManualResetEventSlim _reset = new ManualResetEventSlim(false);

        private Sandbox.V5.MPMCQueue _queue;
        private Thread[] _threads;
        private Thread[] _threadsConsumers;

        public void Setup()
        {
            _queue = new Sandbox.V5.MPMCQueue(_bufferSize);
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
                            throw new Exception();
                        }
                    }
                });
                thread.Start();
                threads[i] = thread;
            }
            return threads;
        }

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
        }
    }
}