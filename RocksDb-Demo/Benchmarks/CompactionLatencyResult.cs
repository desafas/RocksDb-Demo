using System.Diagnostics;

namespace RocksDb_Demo.Benchmarks;

internal class LatencyStats
{
    public static readonly LatencyStats Empty = new();

    private LatencyStats() { }

    public LatencyStats(long[] ticks)
    {
        Count = ticks.Length;
        if (Count == 0) return;
        Array.Sort(ticks);
        P50Ms = TicksToMs(ticks[(int)(Count * 0.50)]);
        P95Ms = TicksToMs(ticks[(int)(Count * 0.95)]);
        P99Ms = TicksToMs(ticks[(int)(Count * 0.99)]);
        P999Ms = TicksToMs(ticks[(int)(Count * 0.999)]);
    }

    private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    public long Count { get; }
    public double P50Ms { get; }
    public double P95Ms { get; }
    public double P99Ms { get; }
    public double P999Ms { get; }
}

internal class CompactionLatencyResult
{
    public required string Label { get; init; }
    public required int FlushCount { get; init; }
    public required int CompactionCount { get; init; }
    public required LatencyStats Reads { get; init; }
    public required LatencyStats Writes { get; init; }
    public required GcStats Gc { get; init; }
    public required double TotalMs { get; init; }
}
