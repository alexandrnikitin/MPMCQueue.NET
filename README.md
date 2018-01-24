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
        var buffer = _buffer; // prefetch the buffer pointer
        var pos = _enqueuePos; // fetch the current position where to enqueue the item
        var index = pos & _bufferMask; // precalculate the index in the buffer for that position
        var cell = buffer[index]; // fetch the cell by the index
        // If its sequence wasn't touched by other producers
        // and we can increment the enqueue position
        if (cell.Sequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
        {
            // write the item we want to enqueue
            buffer[index].Element = item;
            // bump the sequence
            Volatile.Write(ref buffer[index].Sequence, pos + 1);
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
            result = cell.Element;
            // no more reference the dequeue data
            buffer[index].Element = null;
            // update for the next round of the buffer
            Volatile.Write(ref buffer[index].Sequence, pos + bufferMask + 1);
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

### Assembly (RyuJIT x64, clrjit-v4.6.1586.0)

`MPMCQueue.NET.MPMCQueue.TryEnqueue(System.Object)`

```
var buffer = _buffer;
>>> 
00007ffb`3a790660 57              push    rdi
00007ffb`3a790661 56              push    rsi
00007ffb`3a790662 53              push    rbx
00007ffb`3a790663 4883ec20        sub     rsp,20h
00007ffb`3a790667 4c8b4108        mov     r8,qword ptr [rcx+8]

var pos = _enqueuePos;
00007ffb`3a79066b 8b7148          mov     esi,dword ptr [rcx+48h]

var index = pos & _bufferMask;
00007ffb`3a79066e 448bce          mov     r9d,esi
00007ffb`3a790671 44234910        and     r9d,dword ptr [rcx+10h]

var cell = buffer[index];
00007ffb`3a790675 453b4808        cmp     r9d,dword ptr [r8+8]
00007ffb`3a790679 735e            jae     00007ffb`3a7906d9
00007ffb`3a79067b 4963c1          movsxd  rax,r9d
00007ffb`3a79067e 48c1e004        shl     rax,4
00007ffb`3a790682 498d7c0010      lea     rdi,[r8+rax+10h]
00007ffb`3a790687 488bc7          mov     rax,rdi
00007ffb`3a79068a 8b5808          mov     ebx,dword ptr [rax+8]

if (cell.Sequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
00007ffb`3a79068d 3bde            cmp     ebx,esi
00007ffb`3a79068f 753a            jne     00007ffb`3a7906cb
00007ffb`3a790691 4c8d5148        lea     r10,[rcx+48h]
00007ffb`3a790695 448d5e01        lea     r11d,[rsi+1]
00007ffb`3a790699 8bc6            mov     eax,esi
00007ffb`3a79069b f0450fb11a      lock cmpxchg dword ptr [r10],r11d
00007ffb`3a7906a0 3bc6            cmp     eax,esi
00007ffb`3a7906a2 7527            jne     00007ffb`3a7906cb

buffer[index].Element = item;
00007ffb`3a7906a4 4963c9          movsxd  rcx,r9d
00007ffb`3a7906a7 48c1e104        shl     rcx,4
00007ffb`3a7906ab 498d4c0810      lea     rcx,[r8+rcx+10h]
00007ffb`3a7906b0 e83b37605f      call    clr+0x3df0 (00007ffb`99d93df0) (JitHelp: CORINFO_HELP_ASSIGN_REF)

Volatile.Write(ref buffer[index].Sequence, pos + 1);
00007ffb`3a7906b5 488d4708        lea     rax,[rdi+8]
00007ffb`3a7906b9 8d5601          lea     edx,[rsi+1]
00007ffb`3a7906bc 8910            mov     dword ptr [rax],edx

return true;
00007ffb`3a7906be b801000000      mov     eax,1

src\MPMCQueue.NET\MPMCQueue.cs @ 54: (return false;)
00007ffb`3a7906c3 4883c420        add     rsp,20h
00007ffb`3a7906c7 5b              pop     rbx
00007ffb`3a7906c8 5e              pop     rsi
00007ffb`3a7906c9 5f              pop     rdi
00007ffb`3a7906ca c3              ret

if (cell.Sequence < pos)
00007ffb`3a7906cb 3bde            cmp     ebx,esi
00007ffb`3a7906cd 7d98            jge     00007ffb`3a790667

src\MPMCQueue.NET\MPMCQueue.cs @ 54: (return false;)
00007ffb`3a7906cf 33c0            xor     eax,eax
00007ffb`3a7906d1 4883c420        add     rsp,20h
00007ffb`3a7906d5 5b              pop     rbx
00007ffb`3a7906d6 5e              pop     rsi
00007ffb`3a7906d7 5f              pop     rdi
00007ffb`3a7906d8 c3              ret

src\MPMCQueue.NET\MPMCQueue.cs @ 41: 
00007ffb`3a7906d9 e8e21caa5f      call    clr!TranslateSecurityAttributes+0x88050 (00007ffb`9a2323c0) (JitHelp: CORINFO_HELP_RNGCHKFAIL)
00007ffb`3a7906de cc              int     3
```

`MPMCQueue.NET.MPMCQueue.TryDequeue(System.Object ByRef)`

```
var buffer = _buffer;
>>> 
00007ffb`3a790700 4157            push    r15
00007ffb`3a790702 4156            push    r14
00007ffb`3a790704 4154            push    r12
00007ffb`3a790706 57              push    rdi
00007ffb`3a790707 56              push    rsi
00007ffb`3a790708 55              push    rbp
00007ffb`3a790709 53              push    rbx
00007ffb`3a79070a 4883ec20        sub     rsp,20h
00007ffb`3a79070e 4c8bc2          mov     r8,rdx
00007ffb`3a790711 488b7108        mov     rsi,qword ptr [rcx+8]

var bufferMask = _bufferMask;
00007ffb`3a790715 8b7910          mov     edi,dword ptr [rcx+10h]

var pos = _dequeuePos;
00007ffb`3a790718 8b9988000000    mov     ebx,dword ptr [rcx+88h]

var index = pos & bufferMask;
00007ffb`3a79071e 8beb            mov     ebp,ebx
00007ffb`3a790720 23ef            and     ebp,edi

var cell = buffer[index];
00007ffb`3a790722 3b6e08          cmp     ebp,dword ptr [rsi+8]
00007ffb`3a790725 0f8384000000    jae     00007ffb`3a7907af
00007ffb`3a79072b 4863c5          movsxd  rax,ebp
00007ffb`3a79072e 48c1e004        shl     rax,4
00007ffb`3a790732 4c8d740610      lea     r14,[rsi+rax+10h]
00007ffb`3a790737 498bc6          mov     rax,r14
00007ffb`3a79073a 488b10          mov     rdx,qword ptr [rax]
00007ffb`3a79073d 448b7808        mov     r15d,dword ptr [rax+8]

if (cell.Sequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
00007ffb`3a790741 448d6301        lea     r12d,[rbx+1]
00007ffb`3a790745 453bfc          cmp     r15d,r12d
00007ffb`3a790748 7546            jne     00007ffb`3a790790
00007ffb`3a79074a 4c8d8988000000  lea     r9,[rcx+88h]
00007ffb`3a790751 8bc3            mov     eax,ebx
00007ffb`3a790753 f0450fb121      lock cmpxchg dword ptr [r9],r12d
00007ffb`3a790758 3bc3            cmp     eax,ebx
00007ffb`3a79075a 7534            jne     00007ffb`3a790790

result = cell.Element;
00007ffb`3a79075c 498bc8          mov     rcx,r8
00007ffb`3a79075f e85c36605f      call    clr+0x3dc0 (00007ffb`99d93dc0) (JitHelp: CORINFO_HELP_CHECKED_ASSIGN_REF)

buffer[index].Element = null;
00007ffb`3a790764 4863c5          movsxd  rax,ebp
00007ffb`3a790767 48c1e004        shl     rax,4
00007ffb`3a79076b 33d2            xor     edx,edx
00007ffb`3a79076d 4889540610      mov     qword ptr [rsi+rax+10h],rdx

Volatile.Write(ref buffer[index].Sequence, pos + bufferMask + 1);
00007ffb`3a790772 498d4608        lea     rax,[r14+8]
00007ffb`3a790776 03df            add     ebx,edi
00007ffb`3a790778 ffc3            inc     ebx
00007ffb`3a79077a 8918            mov     dword ptr [rax],ebx

return true;
00007ffb`3a79077c b801000000      mov     eax,1

src\MPMCQueue.NET\MPMCQueue.cs @ 79: (return false;)
00007ffb`3a790781 4883c420        add     rsp,20h
00007ffb`3a790785 5b              pop     rbx
00007ffb`3a790786 5d              pop     rbp
00007ffb`3a790787 5e              pop     rsi
00007ffb`3a790788 5f              pop     rdi
00007ffb`3a790789 415c            pop     r12
00007ffb`3a79078b 415e            pop     r14
00007ffb`3a79078d 415f            pop     r15
00007ffb`3a79078f c3              ret

if (cell.Sequence < pos + 1)
00007ffb`3a790790 453bfc          cmp     r15d,r12d
00007ffb`3a790793 0f8d78ffffff    jge     00007ffb`3a790711

result = default(object);
00007ffb`3a790799 33c0            xor     eax,eax
00007ffb`3a79079b 498900          mov     qword ptr [r8],rax

src\MPMCQueue.NET\MPMCQueue.cs @ 79: (return false;)
00007ffb`3a79079e 33c0            xor     eax,eax
00007ffb`3a7907a0 4883c420        add     rsp,20h
00007ffb`3a7907a4 5b              pop     rbx
00007ffb`3a7907a5 5d              pop     rbp
00007ffb`3a7907a6 5e              pop     rsi
00007ffb`3a7907a7 5f              pop     rdi
00007ffb`3a7907a8 415c            pop     r12
00007ffb`3a7907aa 415e            pop     r14
00007ffb`3a7907ac 415f            pop     r15
00007ffb`3a7907ae c3              ret

src\MPMCQueue.NET\MPMCQueue.cs @ 63:
00007ffb`3a7907af e80c1caa5f      call    clr!TranslateSecurityAttributes+0x88050 (00007ffb`9a2323c0) (JitHelp: CORINFO_HELP_RNGCHKFAIL)
00007ffb`3a7907b4 cc              int     3
```


  [1024-mpmc]: http://www.1024cores.net/home/lock-free-algorithms/queues/bounded-mpmc-queue
  [false-sharing]: http://mechanical-sympathy.blogspot.lt/2011/07/false-sharing.html
  [memory-barriers-in-dot-net]: http://afana.me/archive/2015/07/10/memory-barriers-in-dot-net.aspx/