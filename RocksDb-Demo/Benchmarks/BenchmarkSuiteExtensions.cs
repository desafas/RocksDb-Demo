using RocksDb_Demo.Models;
using RocksDb_Demo.Repositories;

namespace RocksDb_Demo.Benchmarks;

internal static class BenchmarkSuiteExtensions
{
    public static BenchmarkResult[] RunBulkRead(
        ICharacterRepository[] repos, string[] labels, long count, ICharacterRepository[] warmableRepos, int batchSize)
    {
        Console.WriteLine($"Running bulk read benchmarks (batch={batchSize:N0})...");
        var results = new List<BenchmarkResult>();
        foreach (var (repo, label) in repos.Zip(labels))
        {
            if (warmableRepos.Contains(repo))
                BenchmarkRunner.RunBulkRead(repo, count, batchSize, label, isWarmup: true);
            results.Add(BenchmarkRunner.RunBulkRead(repo, count, batchSize, label));
        }

        return [.. results];
    }

    public static BenchmarkResult[] RunRandom(
        ICharacterRepository[] repos, string[] labels, long count, ICharacterRepository[] warmableRepos)
    {
        Console.WriteLine("Running random benchmarks...");
        var results = new List<BenchmarkResult>();
        foreach (var (repo, label) in repos.Zip(labels))
        {
            if (warmableRepos.Contains(repo))
                BenchmarkRunner.Run(repo, count, label, true, true);
            results.Add(BenchmarkRunner.Run(repo, count, label, true));
        }

        return [.. results];
    }

    public static async Task<BenchmarkResult[][]> RunConcurrent(
        ICharacterRepository[] repos, string[] labels, long count, ICharacterRepository[] warmableRepos,
        int[] threadCounts)
    {
        WarmCaches(warmableRepos, count);
        Console.WriteLine("Running concurrent random read benchmarks...");
        var concurrentResults = new BenchmarkResult[threadCounts.Length][];
        for (var i = 0; i < threadCounts.Length; i++)
        {
            var tc = threadCounts[i];
            var results = new List<BenchmarkResult>();
            foreach (var (repo, label) in repos.Zip(labels))
                results.Add(await BenchmarkRunner.RunConcurrent(repo, count, tc, label, true));
            concurrentResults[i] = [.. results];
        }

        return concurrentResults;
    }

    public static async Task<BenchmarkResult[]> RunMixed(
        ICharacterRepository[] repos, string[] labels, long count, ICharacterRepository[] warmableRepos,
        PlayerCharacter[] writePool, int readerCount, int writerCount)
    {
        WarmCaches(warmableRepos, count);
        Console.WriteLine("Running mixed read/write benchmarks...");
        var results = new List<BenchmarkResult>();
        foreach (var (repo, label) in repos.Zip(labels))
            results.Add(
                await BenchmarkRunner.RunMixedReadWrite(repo, writePool, count, readerCount, writerCount, label));
        return [.. results];
    }

    public static async Task<CompactionLatencyResult[]> RunCompactionLatency(
        ICharacterRepository[] repos, string[] labels, PlayerCharacter[] writePool,
        int readerCount, int writerCount)
    {
        Console.WriteLine("Running compaction latency benchmarks...");
        var results = new List<CompactionLatencyResult>();
        foreach (var (repo, label) in repos.Zip(labels))
        {
            var monitor = (ICompactionMonitorable)repo;
            results.Add(await BenchmarkRunner.RunCompactionLatency(
                repo, writePool, readerCount, writerCount,
                () => monitor.IsFlushActive,
                monitor.GetCfStats,
                label));
        }
        return [.. results];
    }

    public static async Task<WriteBenchmarkResult[][]> RunWriteUpdate(
        ICharacterRepository[] repos, string[] labels,
        Dictionary<long, PlayerCharacter> baseCharacters,
        PlayerCharacter[] updatePool,
        int[] batchSizes, int threadCount)
    {
        Console.WriteLine("Running update write benchmarks...");
        var results = new WriteBenchmarkResult[batchSizes.Length][];
        for (var b = 0; b < batchSizes.Length; b++)
        {
            results[b] = new WriteBenchmarkResult[repos.Length];
            for (var r = 0; r < repos.Length; r++)
            {
                var repo = repos[r];
                repo.Truncate();
                repo.Initialize(baseCharacters);
                (repo as ISettleable)?.Settle();
                results[b][r] = await BenchmarkRunner.RunBatchedWrites(
                    repo, updatePool, batchSizes[b], threadCount, labels[r]);
            }
        }
        return results;
    }

    public static async Task<WriteBenchmarkResult[][]> RunWriteInsert(
        ICharacterRepository[] repos, string[] labels,
        Dictionary<long, PlayerCharacter> baseCharacters,
        PlayerCharacter[] insertPool,
        int[] batchSizes, int threadCount)
    {
        Console.WriteLine("Running insert write benchmarks...");
        var results = new WriteBenchmarkResult[batchSizes.Length][];
        for (var b = 0; b < batchSizes.Length; b++)
        {
            results[b] = new WriteBenchmarkResult[repos.Length];
            for (var r = 0; r < repos.Length; r++)
            {
                var repo = repos[r];
                repo.Truncate();
                repo.Initialize(baseCharacters);
                (repo as ISettleable)?.Settle();
                results[b][r] = await BenchmarkRunner.RunBatchedWrites(
                    repo, insertPool, batchSizes[b], threadCount, labels[r]);
            }
        }
        return results;
    }

    private static void WarmCaches(ICharacterRepository[] warmableRepos, long count)
    {
        Console.WriteLine("Warming caches...");
        foreach (var repo in warmableRepos)
            BenchmarkRunner.Run(repo, count, "", isWarmup: true);
    }
}