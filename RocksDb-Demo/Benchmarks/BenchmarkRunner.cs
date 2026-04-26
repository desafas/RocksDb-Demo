using System.Diagnostics;
using RocksDb_Demo.Models;
using RocksDb_Demo.Repositories;

namespace RocksDb_Demo.Benchmarks;

internal static class BenchmarkRunner
{
    public static BenchmarkResult Run(ICharacterRepository repo, long count, string label, bool randomize = false,
        bool isWarmup = false)
    {
#if DEBUG
        if (!isWarmup)
            Console.WriteLine(
                "  WARNING: running in Debug mode — results will be skewed. Use 'dotnet run -c Release'.");
#endif
        var ids = BuildIds(count, randomize);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (!isWarmup)
            WindowsPageCacheFlusher.Flush();

        if (!isWarmup)
            Console.WriteLine($"  Running {label}...");

        var sw = Stopwatch.StartNew();

        foreach (var id in ids)
            repo.GetCharacter(id);

        sw.Stop();
        return new BenchmarkResult(label, count, sw.Elapsed.TotalMilliseconds);
    }

    public static async Task<BenchmarkResult> RunConcurrent(ICharacterRepository repo, long count, int threadCount,
        string label, bool randomize = false, bool isWarmup = false)
    {
#if DEBUG
        if (!isWarmup)
            Console.WriteLine(
                "  WARNING: running in Debug mode — results will be skewed. Use 'dotnet run -c Release'.");
#endif
        var ids = BuildIds(count, randomize);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (!isWarmup)
            WindowsPageCacheFlusher.Flush();

        if (!isWarmup)
            Console.WriteLine($"  Running {label} ({threadCount} thread{(threadCount == 1 ? "" : "s")})...");

        var chunkSize = (int)Math.Ceiling((double)count / threadCount);

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threadCount).Select(t =>
        {
            var start = t * chunkSize;
            var length = (int)Math.Min(chunkSize, count - start);
            ReadOnlyMemory<long> chunk = new(ids, start, length);
            return Task.Run(() =>
            {
                foreach (var id in chunk.Span)
                    repo.GetCharacter(id);
            });
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        return new BenchmarkResult(label, count, sw.Elapsed.TotalMilliseconds);
    }

    public static async Task<BenchmarkResult> RunMixedReadWrite(ICharacterRepository repo, PlayerCharacter[] writePool,
        long totalReads, int readerCount, int writerCount, string label, bool isWarmup = false)
    {
#if DEBUG
        if (!isWarmup)
            Console.WriteLine(
                "  WARNING: running in Debug mode — results will be skewed. Use 'dotnet run -c Release'.");
#endif
        var ids = BuildIds(totalReads, true);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (!isWarmup)
            WindowsPageCacheFlusher.Flush();

        if (!isWarmup)
            Console.WriteLine(
                $"  Running {label} ({readerCount} reader{(readerCount == 1 ? "" : "s")}, {writerCount} writer{(writerCount == 1 ? "" : "s")})...");

        var chunkSize = (int)Math.Ceiling((double)totalReads / readerCount);

        using var cts = new CancellationTokenSource();

        var writerTasks = Enumerable.Range(0, writerCount).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var character = writePool[Random.Shared.Next(writePool.Length)];
                repo.UpdateCharacter(character);
            }
        }));

        var sw = Stopwatch.StartNew();

        var readerTasks = Enumerable.Range(0, readerCount).Select(t =>
        {
            var start = t * chunkSize;
            var length = (int)Math.Min(chunkSize, totalReads - start);
            ReadOnlyMemory<long> chunk = new(ids, start, length);
            return Task.Run(() =>
            {
                foreach (var id in chunk.Span)
                    repo.GetCharacter(id);
            });
        });

        await Task.WhenAll(readerTasks);
        sw.Stop();

        cts.Cancel();
        await Task.WhenAll(writerTasks);

        return new BenchmarkResult(label, totalReads, sw.Elapsed.TotalMilliseconds);
    }

    public static void PrintComparison(string title, params BenchmarkResult[] results)
    {
        const int labelWidth = 18;
        const int colWidth = 33;

        Console.WriteLine();
        Console.WriteLine($"=== {title} ({results[0].Count:N0} ops) ===");
        Console.WriteLine();

        var header = "".PadRight(labelWidth) + string.Concat(results.Select(r => r.Label.PadLeft(colWidth)));
        Console.WriteLine(header);
        Console.WriteLine(new string('-', labelWidth + colWidth * results.Length));

        PrintRow("Total time", labelWidth, colWidth, results, r => $"{r.TotalMs:N0} ms");
        PrintRow("Reads/sec", labelWidth, colWidth, results, r => $"{r.ReadsPerSecond:N0}");

        Console.WriteLine();
    }

    public static void PrintConcurrentComparison(string title, int[] threadCounts,
        BenchmarkResult[][] resultsByThreadCount)
    {
        const int labelWidth = 18;
        const int colWidth = 33;

        var repoLabels = resultsByThreadCount[0].Select(r => r.Label).ToArray();
        var totalWidth = labelWidth + colWidth * repoLabels.Length;

        Console.WriteLine();
        Console.WriteLine($"=== {title} ({resultsByThreadCount[0][0].Count:N0} ops) ===");
        Console.WriteLine();

        var header = "".PadRight(labelWidth) + string.Concat(repoLabels.Select(l => l.PadLeft(colWidth)));
        Console.WriteLine(header);
        Console.WriteLine(new string('=', totalWidth));

        for (var i = 0; i < threadCounts.Length; i++)
        {
            Console.WriteLine($" Threads: {threadCounts[i],2}".PadRight(totalWidth, '-'));
            PrintRow("Total time", labelWidth, colWidth, resultsByThreadCount[i], r => $"{r.TotalMs:N0} ms");
            PrintRow("Reads/sec", labelWidth, colWidth, resultsByThreadCount[i], r => $"{r.ReadsPerSecond:N0}");
        }

        Console.WriteLine();
    }

    private static long[] BuildIds(long count, bool randomize)
    {
        var ids = Enumerable.Range(0, (int)count).Select(i => (long)i).ToArray();
        if (randomize)
            Random.Shared.Shuffle(ids);
        return ids;
    }

    private static void PrintRow(string name, int labelWidth, int colWidth,
        BenchmarkResult[] results, Func<BenchmarkResult, string> valueSelector)
    {
        var row = name.PadRight(labelWidth) + string.Concat(results.Select(r => valueSelector(r).PadLeft(colWidth)));
        Console.WriteLine(row);
    }
}