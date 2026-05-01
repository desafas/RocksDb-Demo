namespace RocksDb_Demo.Benchmarks;

internal class BenchmarkResult
{
    public BenchmarkResult(string label, long count, double totalMs, GcStats gc)
    {
        Label = label;
        Count = count;
        TotalMs = totalMs;
        ReadsPerSecond = count / (totalMs / 1000.0);
        Gc = gc;
    }

    public string Label { get; }
    public long Count { get; }
    public double TotalMs { get; }
    public double ReadsPerSecond { get; }
    public GcStats Gc { get; }
}
