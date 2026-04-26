using RocksDb_Demo.Models;
using RocksDb_Demo.Repositories;

namespace RocksDb_Demo.Benchmarks;

internal static class BenchmarkSuiteExtensions
{
    public static BenchmarkResult[] RunSequential(
        ICharacterRepository[] repos, string[] labels, long count, ICharacterRepository[] warmableRepos)
    {
        Console.WriteLine("Running sequential benchmarks...");
        var results = new List<BenchmarkResult>();
        foreach (var (repo, label) in repos.Zip(labels))
        {
            if (warmableRepos.Contains(repo))
                BenchmarkRunner.Run(repo, count, label, isWarmup: true);
            results.Add(BenchmarkRunner.Run(repo, count, label));
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
        WarmCaches(repos, warmableRepos, count);
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
        WarmCaches(repos, warmableRepos, count);
        Console.WriteLine("Running mixed read/write benchmarks...");
        var results = new List<BenchmarkResult>();
        foreach (var (repo, label) in repos.Zip(labels))
            results.Add(
                await BenchmarkRunner.RunMixedReadWrite(repo, writePool, count, readerCount, writerCount, label));
        return [.. results];
    }

    private static void WarmCaches(ICharacterRepository[] repos, ICharacterRepository[] warmableRepos, long count)
    {
        Console.WriteLine("Warming caches...");
        foreach (var repo in warmableRepos)
            BenchmarkRunner.Run(repo, count, "", isWarmup: true);
    }
}