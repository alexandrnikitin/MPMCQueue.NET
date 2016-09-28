using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
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
                Jit = Jit.RyuJit,
                LaunchCount = 2,
                WarmupCount = 20,
                TargetCount = 20,
                Mode = Mode.SingleRun,
            });

            Add(MarkdownExporter.GitHub);
        }
    }
}