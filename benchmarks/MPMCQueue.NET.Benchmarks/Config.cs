using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace MPMCQueue.NET.Benchmarks
{
    public class Config : ManualConfig
    {
        public Config()
        {
            Add(new Job
            {
                Platform = Platform.X64,
                Jit = Jit.LegacyJit,
                LaunchCount = 2,
                WarmupCount = 10,
                TargetCount = 10,
            });

            Add(new Job
            {
                Platform = Platform.X64,
                Jit = Jit.RyuJit,
                LaunchCount = 2,
                WarmupCount = 10,
                TargetCount = 10,
            });
        }
    }
}