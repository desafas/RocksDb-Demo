namespace RocksDb_Demo.Benchmarks;

class BenchmarkResult
{
    public string Label         { get; }
    public long Count           { get; }
    public double TotalMs       { get; }
    public double ReadsPerSecond { get; }

    public BenchmarkResult(string label, long count, double totalMs)
    {
        Label         = label;
        Count         = count;
        TotalMs       = totalMs;
        ReadsPerSecond = count / (totalMs / 1000.0);
    }
}
