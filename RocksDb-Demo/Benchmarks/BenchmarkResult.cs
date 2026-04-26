namespace RocksDb_Demo.Benchmarks;

internal class BenchmarkResult
{
    public BenchmarkResult(string label, long count, double totalMs)
    {
        Label = label;
        Count = count;
        TotalMs = totalMs;
        ReadsPerSecond = count / (totalMs / 1000.0);
    }

    public string Label { get; }
    public long Count { get; }
    public double TotalMs { get; }
    public double ReadsPerSecond { get; }
}