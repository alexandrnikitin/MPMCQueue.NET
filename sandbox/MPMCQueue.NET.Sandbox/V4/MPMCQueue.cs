using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MPMCQueue.NET.Sandbox.V4
{
    public class MPMCQueue<T>
    {
        private readonly Cell[] _buffer;
        private readonly int _bufferMask;

        private int _enqueuePos;
        private int _dequeuePos;


        public MPMCQueue(int bufferSize)
        {
            if (bufferSize < 2) throw new ArgumentException();
            if ((bufferSize & (bufferSize - 1)) != 0) throw new ArgumentException();

            _bufferMask = bufferSize - 1;
            _buffer = new Cell[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                _buffer[i].Sequence = i;
            }

            _enqueuePos = 0;
            _dequeuePos = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(T item)
        {
            var pos = Interlocked.Increment(ref _enqueuePos);
            return _buffer[pos & _bufferMask].Update(pos, item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T result)
        {
            var pos = Interlocked.Increment(ref _dequeuePos);

            do
            {
                var cell = _buffer[pos & _bufferMask];
                var seq = cell.Sequence;
                var diff = seq - (pos + 1);
                if (diff == 0)
                {
                    result = cell.Element;
                    cell.Reset(pos + _bufferMask + 1);
                    return true;
                }
                else if (diff < 0)
                {
                    result = default(T);
                    return false;
                }
            } while (true);
        }

        private struct Cell
        {
            public volatile int Sequence;
            public T Element;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool Update(int pos, T element)
            {
                var diff = Sequence - pos;
                if (diff == 0)
                {
                    Element = element;
                    Sequence = pos + 1;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Reset(int newSequence)
            {
                Element = default(T);
                Sequence = newSequence;
            }
        }
    }
}