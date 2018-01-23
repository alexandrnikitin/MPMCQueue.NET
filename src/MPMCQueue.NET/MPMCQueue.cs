using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MPMCQueue.NET
{
    [StructLayout(LayoutKind.Explicit, Size = 192, CharSet = CharSet.Ansi)]
    public class MPMCQueue
    {
        [FieldOffset(0)]
        private readonly Cell[] _buffer;
        [FieldOffset(8)]
        private readonly int _bufferMask;
        [FieldOffset(64)]
        private int _enqueuePos;
        [FieldOffset(128)]
        private int _dequeuePos;


        public MPMCQueue(int bufferSize)
        {
            if (bufferSize < 2) throw new ArgumentException($"{nameof(bufferSize)} should be greater than or equal to 2");
            if ((bufferSize & (bufferSize - 1)) != 0) throw new ArgumentException($"{nameof(bufferSize)} should be a power of 2");

            _bufferMask = bufferSize - 1;
            _buffer = new Cell[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                _buffer[i] = new Cell(i, null);
            }

            _enqueuePos = 0;
            _dequeuePos = 0;
        }

        public bool TryEnqueue(object item)
        {
            do
            {
                var pos = _enqueuePos;
                var index = pos & _bufferMask;
                var cellSequence = _buffer[index].Sequence;
                if (cellSequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                {
                    _buffer[index].Element = item;
                    Volatile.Write(ref _buffer[index].Sequence, pos + 1); // release fence
                    return true;
                }

                if (cellSequence < pos)
                {
                    return false;
                }
            } while (true);
        }

        public bool TryDequeue(out object result)
        {
            do
            {
                var pos = _dequeuePos;
                var index = pos & _bufferMask;
                var cellSequence = _buffer[index].Sequence;
                if (cellSequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                {
                    result = _buffer[index].Element;
                    _buffer[index].Element = null; // no more reference the dequeue data
                    // result = Interlocked.Exchange(ref _buffer[index].Element, null); // maybe no need use this expensive atomic op, since we don't need ensure atomic here
                    Volatile.Write(ref _buffer[index].Sequence, pos + _bufferMask + 1); // release fence
                    return true;
                }

                if (cellSequence < pos + 1)
                {
                    result = null;
                    return false;
                }
            } while (true);
        }

        private struct Cell
        {
            public int Sequence;
            public object Element;

            public Cell(int sequence, object element)
            {
                Element = element;
                Sequence = sequence;
            }
        }
    }
}