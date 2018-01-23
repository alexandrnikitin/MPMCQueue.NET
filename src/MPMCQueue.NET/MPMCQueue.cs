﻿using System;
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

        public bool TryDequeue(out object result)
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
                    buffer[index] = new Cell(pos + bufferMask + 1, null);
                    return true;
                }

                if (cell.Sequence < pos + 1)
                {
                    result = default(object);
                    return false;
                }
            } while (true);
        }

        [StructLayout(LayoutKind.Explicit, Size = 16, CharSet = CharSet.Ansi)]
        private struct Cell
        {
            [FieldOffset(0)]
            public int Sequence;
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