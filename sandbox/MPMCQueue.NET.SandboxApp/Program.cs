using System;
using System.Runtime.CompilerServices;

namespace MPMCQueue.NET.SandboxApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var queue = new Sandbox.V2.MPMCQueue<bool>(65536);
            Wrapper.Enqueue(queue);
            Wrapper.Dequeue(queue);
            Console.ReadKey();

        }
    }

    public class Wrapper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Enqueue(Sandbox.V2.MPMCQueue<bool> queue)
        {
            queue.TryEnqueue(true);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Dequeue(Sandbox.V2.MPMCQueue<bool> queue)
        {
            bool ret;
            queue.TryDequeue(out ret);
        }
    }

}
