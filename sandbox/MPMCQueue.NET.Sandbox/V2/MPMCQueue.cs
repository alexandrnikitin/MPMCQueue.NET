using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MPMCQueue.NET.Sandbox.V2
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

            var cell = _buffer[pos & _bufferMask];
            var seq = cell.Sequence;
            var diff = seq - pos;
            if (diff == 0)
            {
                cell.Element = item;
                cell.Sequence = pos + 1;
                _buffer[pos & _bufferMask] = cell;
                return true;
            }
            else if (diff < 0)
            {
                return false;
            }
            else
            {
                throw new Exception();
            }
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
                    cell.Element = default(T);
                    cell.Sequence = pos + _bufferMask + 1;
                    _buffer[pos & _bufferMask] = cell;
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
            public int Sequence;
            public T Element;
        }
    }
}