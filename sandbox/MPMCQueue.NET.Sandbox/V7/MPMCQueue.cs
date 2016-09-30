using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MPMCQueue.NET.Sandbox.V7
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class MPMCQueue<T> where T : class 
    {
        private readonly Cell[] _buffer;
        private readonly int _bufferMask;
        private int i1;
        private long l1, l2, l3, l4, l5, l6;
        private int _enqueuePos;
        private int i11;
        private long l11, l12, l13, l14, l15, l16, l17;
        private int _dequeuePos;
        private int i21;
        private long l21, l22, l23, l24, l25, l26, l27;

        public MPMCQueue(int bufferSize)
        {
            if (bufferSize < 2) throw new ArgumentException($"{nameof(bufferSize)} should be greater than 2");
            if ((bufferSize & (bufferSize - 1)) != 0) throw new ArgumentException($"{nameof(bufferSize)} should be a power of 2");

            _bufferMask = bufferSize - 1;
            _buffer = new Cell[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                _buffer[i] = new Cell(i, default(T));
            }

            _enqueuePos = 0;
            _dequeuePos = 0;
        }

        public bool TryEnqueue(T item)
        {
            do
            {
                var buffer = _buffer;
                var pos = _enqueuePos;
                var index = pos & _bufferMask;
                var cell = buffer[index];
                if (cell.Sequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                {
                    Volatile.Write(ref buffer[index].Element, item);
                    buffer[index].Sequence = pos + 1;
                    return true;
                }

                if (cell.Sequence < pos)
                {
                    return false;
                }
            } while (true);
        }

        public bool TryDequeue(out T result)
        {
            do
            {
                var buffer = _buffer;
                var bufferMask = _bufferMask;
                var pos = _dequeuePos;
                var index = pos & bufferMask;
                var cell = buffer[index];
                if (cell.Sequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                {
                    result = Volatile.Read(ref cell.Element);
                    buffer[index] = new Cell(pos + bufferMask + 1, default(T));
                    return true;
                }

                if (cell.Sequence < pos + 1)
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

            public Cell(int sequence, T element)
            {
                Sequence = sequence;
                Element = element;
            }
        }
    }
}