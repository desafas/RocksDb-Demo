namespace RocksDb_Demo.Benchmarks;

internal class WriteBenchmarkResult
{
    public required string Label { get; init; }
    public required long Count { get; init; }
    public required int BatchSize { get; init; }
    public required double TotalMs { get; init; }
    public required LatencyStats Latency { get; init; }

    public double WritesPerSecond => Count / (TotalMs / 1000.0);
}
