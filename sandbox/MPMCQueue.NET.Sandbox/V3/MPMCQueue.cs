using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace MPMCQueue.NET.Sandbox.V3
{
    public unsafe class MPMCQueue<T> : IDisposable
    {
        private readonly int _bufferMask;

        private int _enqueuePos;
        private int _dequeuePos;
        private GCHandle _bufferPinned;
        private readonly void* _bufferPtr;
        private static readonly int SizeOfT = Unsafe.SizeOf<T>();


        public MPMCQueue(int bufferSize)
        {
            if (bufferSize < 2) throw new ArgumentException();
            if ((bufferSize & (bufferSize - 1)) != 0) throw new ArgumentException();

            _bufferMask = bufferSize - 1;
            _bufferPinned = GCHandle.Alloc(new byte[bufferSize * (SizeOfT + sizeof(int))], GCHandleType.Pinned);
            _bufferPtr = _bufferPinned.AddrOfPinnedObject().ToPointer();

            var ptr = (byte*)_bufferPtr;
            for (var i = 0; i < bufferSize; i++)
            {
                Unsafe.Write(ptr, i);
                ptr += sizeof(int) + SizeOfT;
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
                var seq = *(int*)((byte*) _bufferPtr + (pos & _bufferMask)*(SizeOfT + sizeof(int)));
                var diff = seq - pos;
                if (diff == 0)
                {
                    if (Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                    {
                        Unsafe.Write((byte*)_bufferPtr + (pos & _bufferMask) * (SizeOfT + sizeof(int)) + sizeof(int), item);
                        Thread.MemoryBarrier();
                        Unsafe.Write((byte*)_bufferPtr + (pos & _bufferMask) * (SizeOfT + sizeof(int)), pos + 1);
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
                var seq = *(int*)((byte*)_bufferPtr + (pos & _bufferMask) * (SizeOfT + sizeof(int)));
                var diff = seq - (pos + 1);
                if (diff == 0)
                {
                    if (Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                    {
                        result = Unsafe.Read<T>((byte*)_bufferPtr + (pos & _bufferMask) * (SizeOfT + sizeof(int)) + sizeof(int));
                        Thread.MemoryBarrier();
                        Unsafe.Write((byte*)_bufferPtr + (pos & _bufferMask) * (SizeOfT + sizeof(int)), pos + _bufferMask + 1);
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

        public void Dispose()
        {
            _bufferPinned.Free();
        }
    }
}