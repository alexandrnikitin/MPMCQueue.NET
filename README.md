# MPMCQueue.NET
[![Build Status](https://travis-ci.org/alexandrnikitin/MPMCQueue.NET.svg?branch=master)](https://travis-ci.org/alexandrnikitin/MPMCQueue.NET)

Bounded multiple producers multiple consumers queue for .NET

### Overview
This is an attempt to port [the famous Bounded MPMC queue algorithm by Dmitry Vyukov][1024-mpmc] to .NET. All credit goes to Dmitry Vyukov. I let myself quote the description:

>According to the classification it's MPMC, array-based, fails on overflow, does not require GC, w/o priorities, causal FIFO, blocking producers and consumers queue. The algorithm is pretty simple and fast. It's not lockfree in the official meaning, just implemented by means of atomic RMW operations w/o mutexes.

>The cost of enqueue/dequeue is 1 CAS per operation. No amortization, just 1 CAS. No dynamic memory allocation/management during operation. Producers and consumers are separated from each other (as in the two-lock queue), i.e. do not touch the same data while queue is not empty.

### Implementation

The queue class layout is shown below. The `_buffer` field stores enqueued elements and their sequences. It has size that is a power of two. The `_bufferMask` field is used to avoid the expensive modulo operation and use `AND` instead. There's padding applied to avoid [false sharing][false-sharing] of `_enqueuePos` and `_dequeuePos` counters. And [Volatile.Read/Write to suppress memory instructions reordering when read/write cell.Sequence][memory-barriers-in-dot-net].

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
        var pos = _enqueuePos; // fetch the current position where to enqueue the item
        var index = pos & _bufferMask; // precalculate the index in the buffer for that position
        var cellSequence = _buffer[index].Sequence;
        // If its sequence wasn't touched by other producers
        // and we can increment the enqueue position
        if (cellSequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
        {
            // write the item we want to enqueue
            _buffer[index].Element = item;
            // bump the sequence
            Volatile.Write(ref _buffer[index].Sequence, pos + 1); // release fence
            return true;
        }

        // If the queue is full we cannot enqueue and just return false
        if (cellSequence < pos)
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
        var pos = _dequeuePos; // fetch the current position from where we can dequeue an item
        var index = pos & _bufferMask; // precalculate the index in the buffer for that position
        var cellSequence = _buffer[index].Sequence;
        // If its sequence was changed by a producer and wasn't changed by other consumers
        // and we can increment the dequeue position
        if (cellSequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
        {
            // read the item
            result = _buffer[index].Element;
            _buffer[index].Element = null; // no more reference the dequeue data
            // result = Interlocked.Exchange(ref _buffer[index].Element, null); // maybe no need use this expensive atomic op, since we don't need ensure atomic here

            // update for the next round of the buffer
            Volatile.Write(ref _buffer[index].Sequence, pos + _bufferMask + 1); // release fence
            return true;
        }

        // If the queue is empty return false
        if (cellSequence < pos + 1)
        {
            result = null;
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
Processor=Intel(R) Core(TM) i7-4600U CPU 2.10GHz, ProcessorCount=4
Frequency=2630626 ticks, Resolution=380.1377 ns, Timer=TSC
CLR=MS.NET 4.0.30319.42000, Arch=64-bit RELEASE [RyuJIT]
GC=Concurrent Workstation
JitModules=clrjit-v4.6.1586.0
```

`MPMCQueue.NET.MPMCQueue`

Method | NumberOfThreads |     Median |     StdDev |
--------------- |---------------- |----------- |----------- |
**EnqueueDequeue** |               **1** | **24.5941 ns** |  **6.0686 ns** |
**EnqueueDequeue** |               **2** | **45.5109 ns** | **12.0462 ns** |
**EnqueueDequeue** |               **4** | **49.1997 ns** |  **4.3251 ns** |

`System.Collections.Concurrent.ConcurrentQueue`

Method | NumberOfThreads |     Median |    StdDev |
--------------- |---------------- |----------- |---------- |
**EnqueueDequeue** |               **1** | **34.1918 ns** | **0.5379 ns** |
**EnqueueDequeue** |               **2** | **72.1948 ns** | **2.8465 ns** |
**EnqueueDequeue** |               **4** | **63.6846 ns** | **3.6718 ns** |

_`MPMCQueue` shows worse than `ConcurrentQueue` results on many core and multi socket CPUs systems because `cmpxchg` instruction doesn't scale well, [read more](http://joeduffyblog.com/2009/01/08/some-performance-implications-of-cas-operations/)_

### Assembly

`MPMCQueue.NET.MPMCQueue.TryEnqueue(System.Object)`

```
Normal JIT generated code
MPMCQueue.NET.MPMCQueue.TryDequeue(System.Object ByRef)
Begin 00007ffb3a770740, size cb

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 41:
>>> 
00007ffb`3a790690 57              push    rdi
00007ffb`3a790691 56              push    rsi
00007ffb`3a790692 53              push    rbx
00007ffb`3a790693 4883ec20        sub     rsp,20h
00007ffb`3a790697 8b7148          mov     esi,dword ptr [rcx+48h]

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 42:
00007ffb`3a79069a 8bfe            mov     edi,esi
00007ffb`3a79069c 237910          and     edi,dword ptr [rcx+10h]

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 43:
00007ffb`3a79069f 488b4108        mov     rax,qword ptr [rcx+8]
00007ffb`3a7906a3 3b7808          cmp     edi,dword ptr [rax+8]
00007ffb`3a7906a6 736e            jae     00007ffb`3a790716
00007ffb`3a7906a8 4c63c7          movsxd  r8,edi
00007ffb`3a7906ab 49c1e004        shl     r8,4
00007ffb`3a7906af 428b5c0018      mov     ebx,dword ptr [rax+r8+18h]

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 44:
00007ffb`3a7906b4 3bde            cmp     ebx,esi
00007ffb`3a7906b6 7550            jne     00007ffb`3a790708
00007ffb`3a7906b8 4c8d4148        lea     r8,[rcx+48h]
00007ffb`3a7906bc 448d4e01        lea     r9d,[rsi+1]
00007ffb`3a7906c0 8bc6            mov     eax,esi
00007ffb`3a7906c2 f0450fb108      lock cmpxchg dword ptr [r8],r9d
00007ffb`3a7906c7 3bc6            cmp     eax,esi
00007ffb`3a7906c9 753d            jne     00007ffb`3a790708

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 46:
00007ffb`3a7906cb 488b5908        mov     rbx,qword ptr [rcx+8]
00007ffb`3a7906cf 488bcb          mov     rcx,rbx
00007ffb`3a7906d2 8b4108          mov     eax,dword ptr [rcx+8]
00007ffb`3a7906d5 3bf8            cmp     edi,eax
00007ffb`3a7906d7 733d            jae     00007ffb`3a790716
00007ffb`3a7906d9 4863c7          movsxd  rax,edi
00007ffb`3a7906dc 48c1e004        shl     rax,4
00007ffb`3a7906e0 488d4c0110      lea     rcx,[rcx+rax+10h]
00007ffb`3a7906e5 e80637605f      call    clr+0x3df0 (00007ffb`99d93df0) (JitHelp: CORINFO_HELP_ASSIGN_REF)

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 47:
00007ffb`3a7906ea 4863c7          movsxd  rax,edi
00007ffb`3a7906ed 48c1e004        shl     rax,4
00007ffb`3a7906f1 488d440318      lea     rax,[rbx+rax+18h]
00007ffb`3a7906f6 8d5601          lea     edx,[rsi+1]
00007ffb`3a7906f9 8910            mov     dword ptr [rax],edx

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 48:
00007ffb`3a7906fb b801000000      mov     eax,1

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 53:
00007ffb`3a790700 4883c420        add     rsp,20h
00007ffb`3a790704 5b              pop     rbx
00007ffb`3a790705 5e              pop     rsi
00007ffb`3a790706 5f              pop     rdi
00007ffb`3a790707 c3              ret

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 51:
00007ffb`3a790708 3bde            cmp     ebx,esi
00007ffb`3a79070a 7d8b            jge     00007ffb`3a790697

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 53:
00007ffb`3a79070c 33c0            xor     eax,eax
00007ffb`3a79070e 4883c420        add     rsp,20h
00007ffb`3a790712 5b              pop     rbx
00007ffb`3a790713 5e              pop     rsi
00007ffb`3a790714 5f              pop     rdi
00007ffb`3a790715 c3              ret
```

`MPMCQueue.NET.MPMCQueue.TryDequeue(System.Object ByRef)`

```
MPMCQueue.NET.MPMCQueue.TryDequeue(System.Object ByRef)
Begin 00007ffb3a790740, size cb

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 62:
>>> 
00007ffb`3a790740 4156            push    r14
00007ffb`3a790742 57              push    rdi
00007ffb`3a790743 56              push    rsi
00007ffb`3a790744 55              push    rbp
00007ffb`3a790745 53              push    rbx
00007ffb`3a790746 4883ec20        sub     rsp,20h
00007ffb`3a79074a 488bf1          mov     rsi,rcx
00007ffb`3a79074d 488bca          mov     rcx,rdx
00007ffb`3a790750 8bbe88000000    mov     edi,dword ptr [rsi+88h]

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 63:
00007ffb`3a790756 8bdf            mov     ebx,edi
00007ffb`3a790758 235e10          and     ebx,dword ptr [rsi+10h]

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 64:
00007ffb`3a79075b 488b4608        mov     rax,qword ptr [rsi+8]
00007ffb`3a79075f 3b5808          cmp     ebx,dword ptr [rax+8]
00007ffb`3a790762 0f839d000000    jae     00007ffb`3a790805
00007ffb`3a790768 4863d3          movsxd  rdx,ebx
00007ffb`3a79076b 48c1e204        shl     rdx,4
00007ffb`3a79076f 8b6c1018        mov     ebp,dword ptr [rax+rdx+18h]

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 65:
00007ffb`3a790773 448d7701        lea     r14d,[rdi+1]
00007ffb`3a790777 413bee          cmp     ebp,r14d
00007ffb`3a79077a 756e            jne     00007ffb`3a7907ea
00007ffb`3a79077c 488d9688000000  lea     rdx,[rsi+88h]
00007ffb`3a790783 8bc7            mov     eax,edi
00007ffb`3a790785 f0440fb132      lock cmpxchg dword ptr [rdx],r14d
00007ffb`3a79078a 3bc7            cmp     eax,edi
00007ffb`3a79078c 755c            jne     00007ffb`3a7907ea

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 67:
00007ffb`3a79078e 488b5608        mov     rdx,qword ptr [rsi+8]
00007ffb`3a790792 3b5a08          cmp     ebx,dword ptr [rdx+8]
00007ffb`3a790795 736e            jae     00007ffb`3a790805
00007ffb`3a790797 4863c3          movsxd  rax,ebx
00007ffb`3a79079a 48c1e004        shl     rax,4
00007ffb`3a79079e 488b540210      mov     rdx,qword ptr [rdx+rax+10h]
00007ffb`3a7907a3 e81836605f      call    clr+0x3dc0 (00007ffb`99d93dc0) (JitHelp: CORINFO_HELP_CHECKED_ASSIGN_REF)

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 68:
00007ffb`3a7907a8 488b4608        mov     rax,qword ptr [rsi+8]
00007ffb`3a7907ac 488bd0          mov     rdx,rax
00007ffb`3a7907af 8b4a08          mov     ecx,dword ptr [rdx+8]
00007ffb`3a7907b2 3bd9            cmp     ebx,ecx
00007ffb`3a7907b4 734f            jae     00007ffb`3a790805
00007ffb`3a7907b6 4863cb          movsxd  rcx,ebx
00007ffb`3a7907b9 48c1e104        shl     rcx,4
00007ffb`3a7907bd 4533c0          xor     r8d,r8d
00007ffb`3a7907c0 4c89440a10      mov     qword ptr [rdx+rcx+10h],r8

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 70:
00007ffb`3a7907c5 4863d3          movsxd  rdx,ebx
00007ffb`3a7907c8 48c1e204        shl     rdx,4
00007ffb`3a7907cc 488d441018      lea     rax,[rax+rdx+18h]
00007ffb`3a7907d1 8b5610          mov     edx,dword ptr [rsi+10h]
00007ffb`3a7907d4 8d541701        lea     edx,[rdi+rdx+1]
00007ffb`3a7907d8 8910            mov     dword ptr [rax],edx

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 71:
00007ffb`3a7907da b801000000      mov     eax,1

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 77:
00007ffb`3a7907df 4883c420        add     rsp,20h
00007ffb`3a7907e3 5b              pop     rbx
00007ffb`3a7907e4 5d              pop     rbp
00007ffb`3a7907e5 5e              pop     rsi
00007ffb`3a7907e6 5f              pop     rdi
00007ffb`3a7907e7 415e            pop     r14
00007ffb`3a7907e9 c3              ret

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 74:
00007ffb`3a7907ea 413bee          cmp     ebp,r14d
00007ffb`3a7907ed 0f8d5dffffff    jge     00007ffb`3a790750

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 76:
00007ffb`3a7907f3 33c0            xor     eax,eax
00007ffb`3a7907f5 488901          mov     qword ptr [rcx],rax

E:\git\MPMCQueue.NET\src\MPMCQueue.NET\MPMCQueue.cs @ 77:
00007ffb`3a7907f8 33c0            xor     eax,eax
00007ffb`3a7907fa 4883c420        add     rsp,20h
00007ffb`3a7907fe 5b              pop     rbx
00007ffb`3a7907ff 5d              pop     rbp
00007ffb`3a790800 5e              pop     rsi
00007ffb`3a790801 5f              pop     rdi
00007ffb`3a790802 415e            pop     r14
00007ffb`3a790804 c3              ret

```


  [1024-mpmc]: http://www.1024cores.net/home/lock-free-algorithms/queues/bounded-mpmc-queue
  [false-sharing]: http://mechanical-sympathy.blogspot.lt/2011/07/false-sharing.html
  [memory-barriers-in-dot-net]: http://afana.me/archive/2015/07/10/memory-barriers-in-dot-net.aspx/
