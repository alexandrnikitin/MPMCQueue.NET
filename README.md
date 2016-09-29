# MPMCQueue.NET
[![Build Status](https://travis-ci.org/alexandrnikitin/MPMCQueue.NET.svg?branch=master)](https://travis-ci.org/alexandrnikitin/MPMCQueue.NET)

Bounded multiple producers multiple consumers queue for .NET

### Overview
This is an attempt to port [the famous Bounded MPMC queue algorithm by Dmitry Vyukov][1024-mpmc] to .NET. All credit goes to Dmitry Vyukov. I let myself quote the description:

>According to the classification it's MPMC, array-based, fails on overflow, does not require GC, w/o priorities, causal FIFO, blocking producers and consumers queue. The algorithm is pretty simple and fast. It's not lockfree in the official meaning, just implemented by means of atomic RMW operations w/o mutexes.

>The cost of enqueue/dequeue is 1 CAS per operation. No amortization, just 1 CAS. No dynamic memory allocation/management during operation. Producers and consumers are separated from each other (as in the two-lock queue), i.e. do not touch the same data while queue is not empty.

### Implementation

The queue class layout is shown below. The `_buffer` field stores enqueued elements and their sequences. It has size that is a power of two. The `_bufferMask` field is used to avoid the expensive modulo operation and use AND instead. There's padding applied to avoid [false sharing][false-sharing] of `_enqueuePos` and `_dequeuePos`.

```csharp
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

    ...
}
```

The enqueue algorithm:

```csharp
public bool TryEnqueue(object item)
{
    do
    {
        var buffer = _buffer; // prefetch the buffer pointer
        var pos = _enqueuePos; // fetch the current position where to enqueue the item
        var index = pos & _bufferMask; // precalculate the index in the buffer for that position
        var cell = buffer[index]; // fetch the cell by the index
        // If its sequence wasn't touched by other producers
        // and we can increment the enqueue position
        if (cell.Sequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
        {
            // write the item we want to enqueue
            Volatile.Write(ref buffer[index].Element, item);
            // bump the sequence
            buffer[index].Sequence = pos + 1;
            return true;
        }

        // If the queue is full we cannot enqueue and just return false
        if (cell.Sequence < pos)
        {
            return false;
        }

        // repeat the process if other producer managed to enqueue before us
    } while (true);
}
```

The dequeue algorithm:

```csharp
public bool TryDequeue(out object result)
{
    do
    {
        var buffer = _buffer; // prefetch the buffer pointer
        var bufferMask = _bufferMask; // prefetch the buffer mask
        var pos = _dequeuePos; // fetch the current position from where we can dequeue an item
        var index = pos & bufferMask; // precalculate the index in the buffer for that position
        var cell = buffer[index]; // fetch the cell by the index
        // If its sequence was changed by a producer and wasn't changed by other consumers
        // and we can increment the dequeue position
        if (cell.Sequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
        {
            // read the item
            result = Volatile.Read(ref cell.Element);
            // update for the next round of the buffer
            buffer[index] = new Cell(pos + bufferMask + 1, null);
            return true;
        }

        // If the queue is empty return false
        if (cell.Sequence < pos + 1)
        {
            result = default(object);
            return false;
        }

        // repeat the process if other consumer managed to dequeue before us
    } while (true);
}
```

### Benchmarks

```ini
Host Process Environment Information:
BenchmarkDotNet.Core=v0.9.9.0
OS=Microsoft Windows NT 6.2.9200.0
Processor=Intel(R) Xeon(R) CPU E5-2630 v3 2.40GHzIntel(R) Xeon(R) CPU E5-2630 v3 2.40GHz, ProcessorCount=16
Frequency=2341039 ticks, Resolution=427.1608 ns, Timer=TSC
CLR=MS.NET 4.0.30319.42000, Arch=64-bit RELEASE [RyuJIT]
GC=Concurrent Workstation
JitModules=clrjit-v4.6.1590.0
```

`MPMCQueue.NET.MPMCQueue`

Method | NumberOfThreads |      Median |     StdDev |
--------------- |---------------- |------------ |----------- |
**EnqueueDequeue** |               **1** |  **33.8924 ns** |  **9.4806 ns** |
**EnqueueDequeue** |               **2** | **123.5304 ns** | **21.1472 ns** |
**EnqueueDequeue** |               **4** | **199.4333 ns** | **10.0178 ns** |
**EnqueueDequeue** |               **8** | **234.0404 ns** |  **6.4542 ns** |
**EnqueueDequeue** |              **16** | **121.2204 ns** | **21.0024 ns** |

`System.Collections.Concurrent.ConcurrentQueue`

         Method | NumberOfThreads |      Median |    StdDev |
--------------- |---------------- |------------ |---------- |
 **EnqueueDequeue** |               **1** |  **36.1605 ns** | **0.4909 ns** |
 **EnqueueDequeue** |               **2** |  **95.6493 ns** | **1.9413 ns** |
 **EnqueueDequeue** |               **4** | **102.2402 ns** | **2.4507 ns** |
 **EnqueueDequeue** |               **8** | **118.2270 ns** | **2.7861 ns** |
 **EnqueueDequeue** |              **16** | **126.1636 ns** | **7.9490 ns** |


### Assembly

`MPMCQueue.NET.MPMCQueue.TryEnqueue(System.Object)`

```
00007ffe`162b0d40 57              push    rdi
00007ffe`162b0d41 56              push    rsi
00007ffe`162b0d42 55              push    rbp
00007ffe`162b0d43 53              push    rbx
00007ffe`162b0d44 4883ec28        sub     rsp,28h
00007ffe`162b0d48 488b7108        mov     rsi,qword ptr [rcx+8]
00007ffe`162b0d4c 8b7948          mov     edi,dword ptr [rcx+48h]
00007ffe`162b0d4f 8b4110          mov     eax,dword ptr [rcx+10h]
00007ffe`162b0d52 23c7            and     eax,edi
00007ffe`162b0d54 8bd8            mov     ebx,eax
00007ffe`162b0d56 8b4608          mov     eax,dword ptr [rsi+8]
00007ffe`162b0d59 3bd8            cmp     ebx,eax
00007ffe`162b0d5b 735c            jae     00007ffe`162b0db9
00007ffe`162b0d5d 4863c3          movsxd  rax,ebx
00007ffe`162b0d60 48c1e004        shl     rax,4
00007ffe`162b0d64 4c8d440610      lea     r8,[rsi+rax+10h]
00007ffe`162b0d69 498bc0          mov     rax,r8
00007ffe`162b0d6c 8b28            mov     ebp,dword ptr [rax]
00007ffe`162b0d6e 3bef            cmp     ebp,edi
00007ffe`162b0d70 7538            jne     00007ffe`162b0daa
00007ffe`162b0d72 4c8d4948        lea     r9,[rcx+48h]
00007ffe`162b0d76 448d5701        lea     r10d,[rdi+1]
00007ffe`162b0d7a 8bc7            mov     eax,edi
00007ffe`162b0d7c f0450fb111      lock cmpxchg dword ptr [r9],r10d
00007ffe`162b0d81 3bc7            cmp     eax,edi
00007ffe`162b0d83 7525            jne     00007ffe`162b0daa
00007ffe`162b0d85 498d4808        lea     rcx,[r8+8]
00007ffe`162b0d89 e872305f5f      call    clr!JIT_CheckedWriteBarrier (00007ffe`758a3e00)
00007ffe`162b0d8e 8d4701          lea     eax,[rdi+1]
00007ffe`162b0d91 4863d3          movsxd  rdx,ebx
00007ffe`162b0d94 48c1e204        shl     rdx,4
00007ffe`162b0d98 89441610        mov     dword ptr [rsi+rdx+10h],eax
00007ffe`162b0d9c b801000000      mov     eax,1
00007ffe`162b0da1 4883c428        add     rsp,28h
00007ffe`162b0da5 5b              pop     rbx
00007ffe`162b0da6 5d              pop     rbp
00007ffe`162b0da7 5e              pop     rsi
00007ffe`162b0da8 5f              pop     rdi
00007ffe`162b0da9 c3              ret
00007ffe`162b0daa 3bef            cmp     ebp,edi
00007ffe`162b0dac 7d9a            jge     00007ffe`162b0d48
00007ffe`162b0dae 33c0            xor     eax,eax
00007ffe`162b0db0 4883c428        add     rsp,28h
00007ffe`162b0db4 5b              pop     rbx
00007ffe`162b0db5 5d              pop     rbp
00007ffe`162b0db6 5e              pop     rsi
00007ffe`162b0db7 5f              pop     rdi
00007ffe`162b0db8 c3              ret
00007ffe`162b0db9 e8226ea95f      call    clr!JIT_RngChkFail (00007ffe`75d47be0)
00007ffe`162b0dbe cc              int     3
```

`MPMCQueue.NET.MPMCQueue.TryDequeue(System.Object ByRef)`

```
00007ffe`162b0b10 4156            push    r14
00007ffe`162b0b12 57              push    rdi
00007ffe`162b0b13 56              push    rsi
00007ffe`162b0b14 55              push    rbp
00007ffe`162b0b15 53              push    rbx
00007ffe`162b0b16 4883ec20        sub     rsp,20h
00007ffe`162b0b1a 4c8bc2          mov     r8,rdx
00007ffe`162b0b1d 488b4108        mov     rax,qword ptr [rcx+8]
00007ffe`162b0b21 8b7110          mov     esi,dword ptr [rcx+10h]
00007ffe`162b0b24 8bb988000000    mov     edi,dword ptr [rcx+88h]
00007ffe`162b0b2a 8bd7            mov     edx,edi
00007ffe`162b0b2c 23d6            and     edx,esi
00007ffe`162b0b2e 448b4808        mov     r9d,dword ptr [rax+8]
00007ffe`162b0b32 413bd1          cmp     edx,r9d
00007ffe`162b0b35 736b            jae     00007ffe`162b0ba2
00007ffe`162b0b37 4863d2          movsxd  rdx,edx
00007ffe`162b0b3a 48c1e204        shl     rdx,4
00007ffe`162b0b3e 488d5c1010      lea     rbx,[rax+rdx+10h]
00007ffe`162b0b43 488bc3          mov     rax,rbx
00007ffe`162b0b46 8b28            mov     ebp,dword ptr [rax]
00007ffe`162b0b48 488b5008        mov     rdx,qword ptr [rax+8]
00007ffe`162b0b4c 448d7701        lea     r14d,[rdi+1]
00007ffe`162b0b50 413bee          cmp     ebp,r14d
00007ffe`162b0b53 7536            jne     00007ffe`162b0b8b
00007ffe`162b0b55 4c8d8988000000  lea     r9,[rcx+88h]
00007ffe`162b0b5c 8bc7            mov     eax,edi
00007ffe`162b0b5e f0450fb131      lock cmpxchg dword ptr [r9],r14d
00007ffe`162b0b63 3bc7            cmp     eax,edi
00007ffe`162b0b65 7524            jne     00007ffe`162b0b8b
00007ffe`162b0b67 498bc8          mov     rcx,r8
00007ffe`162b0b6a e891325f5f      call    clr!JIT_CheckedWriteBarrier (00007ffe`758a3e00)
00007ffe`162b0b6f 8d443701        lea     eax,[rdi+rsi+1]
00007ffe`162b0b73 33d2            xor     edx,edx
00007ffe`162b0b75 8903            mov     dword ptr [rbx],eax
00007ffe`162b0b77 48895308        mov     qword ptr [rbx+8],rdx
00007ffe`162b0b7b b801000000      mov     eax,1
00007ffe`162b0b80 4883c420        add     rsp,20h
00007ffe`162b0b84 5b              pop     rbx
00007ffe`162b0b85 5d              pop     rbp
00007ffe`162b0b86 5e              pop     rsi
00007ffe`162b0b87 5f              pop     rdi
00007ffe`162b0b88 415e            pop     r14
00007ffe`162b0b8a c3              ret
00007ffe`162b0b8b 413bee          cmp     ebp,r14d
00007ffe`162b0b8e 7d8d            jge     00007ffe`162b0b1d
00007ffe`162b0b90 33c0            xor     eax,eax
00007ffe`162b0b92 498900          mov     qword ptr [r8],rax
00007ffe`162b0b95 33c0            xor     eax,eax
00007ffe`162b0b97 4883c420        add     rsp,20h
00007ffe`162b0b9b 5b              pop     rbx
00007ffe`162b0b9c 5d              pop     rbp
00007ffe`162b0b9d 5e              pop     rsi
00007ffe`162b0b9e 5f              pop     rdi
00007ffe`162b0b9f 415e            pop     r14
00007ffe`162b0ba1 c3              ret
00007ffe`162b0ba2 e83970a95f      call    clr!JIT_RngChkFail (00007ffe`75d47be0)
00007ffe`162b0ba7 cc              int     3
```


  [1024-mpmc]: http://www.1024cores.net/home/lock-free-algorithms/queues/bounded-mpmc-queue
  [false-sharing]: http://mechanical-sympathy.blogspot.lt/2011/07/false-sharing.html
