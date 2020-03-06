using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Runtime.CompilerServices;

namespace MPMCQueue.NET
{
    [StructLayout(LayoutKind.Explicit, Size = 336)]
    public class MPMCQueue
    {
        /// <summary>
        /// 128 bytes cache line already exists in some CPUs.
        /// </summary>
        /// <remarks>
        /// Also "the spatial prefetcher strives to keep pairs of cache lines in the L2 cache."
        /// https://stackoverflow.com/questions/29199779/false-sharing-and-128-byte-alignment-padding
        /// </remarks>
        internal const int SAFE_CACHE_LINE = 128;

        [FieldOffset(SAFE_CACHE_LINE)]
        private readonly Cell[] _enqueueBuffer;

        [FieldOffset(SAFE_CACHE_LINE + 8)]
        private volatile int _enqueuePos;

        // Separate access to buffers from enqueue and dequeue.
        // This removes false sharing and accessing a buffer 
        // reference also prefetches the following Pos with [(64 - (8 + 4 + 4)) = 52]/64 probability.

        [FieldOffset(SAFE_CACHE_LINE * 2)]
        private readonly Cell[] _dequeueBuffer;

        [FieldOffset(SAFE_CACHE_LINE * 2 + 8)]
        private volatile int _dequeuePos;

        public MPMCQueue(int bufferSize)
        {
            if (bufferSize < 2) throw new ArgumentException($"{nameof(bufferSize)} should be greater than or equal to 2");
            if ((bufferSize & (bufferSize - 1)) != 0) throw new ArgumentException($"{nameof(bufferSize)} should be a power of 2");

            _enqueueBuffer = new Cell[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                _enqueueBuffer[i] = new Cell(i, null);
            }

            _dequeueBuffer = _enqueueBuffer;
            _enqueuePos = 0;
            _dequeuePos = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(object item)
        {
            var spinner = new SpinWait();
            do
            {
                var buffer = _enqueueBuffer;
                var pos = _enqueuePos;
                var index = pos & (buffer.Length - 1);
                ref var cell = ref buffer[index];
                if (cell.Sequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                {
                    cell.Element = item;
                    cell.Sequence = pos + 1;
                    return true;
                }

                if (cell.Sequence < pos)
                {
                    return false;
                }

                spinner.SpinOnce();
            } while (true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out object result)
        {
            result = null;
            var spinner = new SpinWait();
            do
            {
                var buffer = _dequeueBuffer;
                var pos = _dequeuePos;
                var index = pos & (buffer.Length - 1);
                ref var cell = ref buffer[index];
                if (cell.Sequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                {
                    result = cell.Element;
                    cell.Element = null;
                    cell.Sequence = pos + buffer.Length;
                    break;
                }

                if (cell.Sequence < pos + 1)
                {
                    break;
                }

                spinner.SpinOnce();
            } while (true);

            return result != null;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct Cell
        {
            [FieldOffset(0)]
            public volatile int Sequence;

            [FieldOffset(8)]
            public object Element;

            public Cell(int sequence, object element)
            {
                Sequence = sequence;
                Element = element;
            }
        }
    }
}
