using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace MPMCQueue.NET.Benchmarks.Configs
{
    public class SingleRunConfig : ManualConfig
    {
        public SingleRunConfig()
        {
            Add(new Job
            {
                Platform = Platform.X64,
                Jit = Jit.LegacyJit,
                LaunchCount = 2,
                WarmupCount = 10,
                TargetCount = 10,
                Mode = Mode.SingleRun,
            });

            Add(new Job
            {
                Platform = Platform.X64,
                Jit = Jit.RyuJit,
                LaunchCount = 2,
                WarmupCount = 10,
                TargetCount = 10,
                Mode = Mode.SingleRun,
            });
        }
    }
}