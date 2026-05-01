using System.Diagnostics;

namespace RocksDb_Demo.Benchmarks;

internal readonly record struct GcStats(
    long AllocatedBytes,
    int Gen0, int Gen1, int Gen2,
    TimeSpan CpuTime)
{
    public static GcStats Capture() => new(
        GC.GetTotalAllocatedBytes(),
        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
        Process.GetCurrentProcess().TotalProcessorTime);

    public GcStats Delta(GcStats after) => new(
        after.AllocatedBytes - AllocatedBytes,
        after.Gen0 - Gen0, after.Gen1 - Gen1, after.Gen2 - Gen2,
        after.CpuTime - CpuTime);
}
