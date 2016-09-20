using System;
using System.Runtime.CompilerServices;

namespace MPMCQueue.NET.SandboxApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var queue = new Sandbox.V2.MPMCQueue<bool>(65536);
            Wrapper.CanEnqueueAndDequeue(queue);
            Console.ReadKey();

        }
    }

    public class Wrapper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CanEnqueueAndDequeue(Sandbox.V2.MPMCQueue<bool> queue)
        {
            queue.TryEnqueue(true);
        }
    }

}
