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
            do
            {
                var pos = _enqueuePos;
                var cell = _buffer[pos & _bufferMask];
                var seq = cell.Sequence;
                var diff = seq - pos;
                if (diff == 0)
                {
                    if (Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                    {
                        cell.Element = item;
                        cell.Sequence = pos + 1;
                        _buffer[pos & _bufferMask] = cell;
                        return true;
                    }
                }
                else if (diff < 0)
                {
                    return false;
                }
            } while (true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T result)
        {
            do
            {
                var pos = _dequeuePos;
                var cell = _buffer[pos & _bufferMask];
                var seq = cell.Sequence;
                var diff = seq - (pos + 1);
                if (diff == 0)
                {
                    if (Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                    {
                        result = cell.Element;
                        cell.Sequence = pos + _bufferMask + 1;
                        _buffer[pos & _bufferMask] = cell;
                        return true;
                    }
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
        }
    }
}