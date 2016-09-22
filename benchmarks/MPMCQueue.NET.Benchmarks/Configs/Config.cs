using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;

namespace MPMCQueue.NET.Benchmarks.Configs
{
    public class Config : ManualConfig
    {
        public Config()
        {
            Add(new Job
            {
                Platform = Platform.X64,
                Jit = Jit.LegacyJit,
                LaunchCount = 5,
                WarmupCount = 20,
                TargetCount = 20,
            });

            Add(new Job
            {
                Platform = Platform.X64,
                Jit = Jit.RyuJit,
                LaunchCount = 5,
                WarmupCount = 20,
                TargetCount = 20,
            });

            Add(MarkdownExporter.GitHub);
        }
    }
}