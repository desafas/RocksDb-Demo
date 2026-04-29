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
        })).ToArray();

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

        await cts.CancelAsync();
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

    public static async Task<CompactionLatencyResult> RunCompactionLatency(
        ICharacterRepository repo,
        PlayerCharacter[] writePool,
        int readerCount,
        int writerCount,
        Func<bool> isFlushActive,
        Func<string?> getCfStats,
        string label)
    {
#if DEBUG
        Console.WriteLine(
            "  WARNING: running in Debug mode — results will be skewed. Use 'dotnet run -c Release'.");
#endif
        var count = writePool.Length;

        var readIds = Enumerable.Range(0, count).Select(i => (long)i).ToArray();
        Random.Shared.Shuffle(readIds);

        var shuffledPool = (PlayerCharacter[])writePool.Clone();
        Random.Shared.Shuffle(shuffledPool);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        WindowsPageCacheFlusher.Flush();

        Console.WriteLine($"  Running {label}...");

        var cfStatsBefore = getCfStats();

        var readLatencies = new long[count];
        var writeLatencies = new long[count];

        var wasFlushActive = false;
        var flushCount = 0;
        using var monitorCts = new CancellationTokenSource();

        var monitorTask = Task.Run(async () =>
        {
            wasFlushActive = isFlushActive();

            while (!monitorCts.Token.IsCancellationRequested)
            {
                var flushing = isFlushActive();

                if (!flushing && wasFlushActive)
                    Interlocked.Increment(ref flushCount);
                wasFlushActive = flushing;

                try { await Task.Delay(10, monitorCts.Token); }
                catch (OperationCanceledException) { break; }
            }
        });

        var readChunk = (int)Math.Ceiling((double)count / readerCount);
        var writeChunk = (int)Math.Ceiling((double)count / writerCount);

        var readerTaskArray = Enumerable.Range(0, readerCount).Select(t =>
        {
            var start = t * readChunk;
            var length = (int)Math.Min(readChunk, count - start);
            ReadOnlyMemory<long> chunk = new(readIds, start, length);
            var offset = start;
            return Task.Run(() =>
            {
                var span = chunk.Span;
                for (var i = 0; i < span.Length; i++)
                {
                    var t0 = Stopwatch.GetTimestamp();
                    repo.GetCharacter(span[i]);
                    var t1 = Stopwatch.GetTimestamp();
                    readLatencies[offset + i] = t1 - t0;
                }
            });
        }).ToArray();

        var writerTaskArray = Enumerable.Range(0, writerCount).Select(t =>
        {
            var start = t * writeChunk;
            var length = (int)Math.Min(writeChunk, count - start);
            ReadOnlyMemory<PlayerCharacter> chunk = new(shuffledPool, start, length);
            var offset = start;
            return Task.Run(() =>
            {
                var span = chunk.Span;
                for (var i = 0; i < span.Length; i++)
                {
                    var t0 = Stopwatch.GetTimestamp();
                    repo.UpdateCharacter(span[i]);
                    var t1 = Stopwatch.GetTimestamp();
                    writeLatencies[offset + i] = t1 - t0;
                }
            });
        }).ToArray();

        await Task.WhenAll(readerTaskArray.Concat(writerTaskArray));

        await monitorCts.CancelAsync();
        await monitorTask;

        var cfStatsAfter = getCfStats();
        var compactionCount = ParseSumCompCount(cfStatsAfter) - ParseSumCompCount(cfStatsBefore);

        return new CompactionLatencyResult
        {
            Label = label,
            FlushCount = flushCount,
            CompactionCount = compactionCount,
            Reads = new LatencyStats(readLatencies.ToList()),
            Writes = new LatencyStats(writeLatencies.ToList()),
        };
    }

    public static void PrintCompactionLatencyComparison(string title, CompactionLatencyResult[] results)
    {
        const int labelWidth = 26;
        const int colWidth = 38;

        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
        Console.WriteLine();

        var header = "".PadRight(labelWidth) + string.Concat(results.Select(r => r.Label.PadLeft(colWidth)));
        Console.WriteLine(header);
        Console.WriteLine(new string('-', labelWidth + colWidth * results.Length));

        PrintCompRow("Flushes", labelWidth, colWidth, results, r => $"{r.FlushCount}");
        PrintCompRow("Compactions", labelWidth, colWidth, results, r => $"{r.CompactionCount}");

        Console.WriteLine();
        Console.WriteLine("READ LATENCY");
        PrintCompRow("  p50", labelWidth, colWidth, results, r => Fmt(r.Reads.P50Ms));
        PrintCompRow("  p95", labelWidth, colWidth, results, r => Fmt(r.Reads.P95Ms));
        PrintCompRow("  p99", labelWidth, colWidth, results, r => Fmt(r.Reads.P99Ms));
        PrintCompRow("  p999", labelWidth, colWidth, results, r => Fmt(r.Reads.P999Ms));

        Console.WriteLine();
        Console.WriteLine("WRITE LATENCY");
        PrintCompRow("  p50", labelWidth, colWidth, results, r => Fmt(r.Writes.P50Ms));
        PrintCompRow("  p95", labelWidth, colWidth, results, r => Fmt(r.Writes.P95Ms));
        PrintCompRow("  p99", labelWidth, colWidth, results, r => Fmt(r.Writes.P99Ms));
        PrintCompRow("  p999", labelWidth, colWidth, results, r => Fmt(r.Writes.P999Ms));

        Console.WriteLine();
    }

    private static string Fmt(double ms) => $"{ms:F3} ms";

    private static void PrintCompRow(string name, int labelWidth, int colWidth,
        CompactionLatencyResult[] results, Func<CompactionLatencyResult, string> valueSelector)
    {
        var row = name.PadRight(labelWidth) + string.Concat(results.Select(r => valueSelector(r).PadLeft(colWidth)));
        Console.WriteLine(row);
    }

    private static int ParseSumCompCount(string? cfstats)
    {
        if (cfstats is null) return 0;
        foreach (var line in cfstats.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("Sum ") && !trimmed.StartsWith("Sum\t")) continue;
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Row format: Sum Files(X/Y) Size Unit Score Read Rn Rnp1 Write Wnew Moved W-Amp Rd Wr Comp(sec) CompMergeCPU(sec) Comp(cnt)
            // Index 16 = Comp(cnt)
            if (parts.Length > 16 && int.TryParse(parts[16], out var count))
                return count;
        }
        return 0;
    }

    public static async Task<WriteBenchmarkResult> RunBatchedWrites(
        ICharacterRepository repo,
        PlayerCharacter[] writePool,
        int batchSize,
        int threadCount,
        string label)
    {
#if DEBUG
        Console.WriteLine(
            "  WARNING: running in Debug mode — results will be skewed. Use 'dotnet run -c Release'.");
#endif
        var count = writePool.Length;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        WindowsPageCacheFlusher.Flush();

        Console.WriteLine($"  Running {label} (batch={batchSize}, {threadCount} threads)...");

        var chunkSize = (int)Math.Ceiling((double)count / threadCount);
        var latencies = batchSize == 1 ? new long[count] : null;

        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, threadCount).Select(t =>
        {
            var start = t * chunkSize;
            var length = (int)Math.Min(chunkSize, count - start);
            return Task.Run(() =>
            {
                if (batchSize == 1)
                {
                    for (var i = 0; i < length; i++)
                    {
                        var t0 = Stopwatch.GetTimestamp();
                        repo.UpdateCharacter(writePool[start + i]);
                        var t1 = Stopwatch.GetTimestamp();
                        latencies![start + i] = t1 - t0;
                    }
                }
                else
                {
                    for (var i = 0; i < length; i += batchSize)
                    {
                        var size = Math.Min(batchSize, length - i);
                        repo.WriteBatch(new ReadOnlyMemory<PlayerCharacter>(writePool, start + i, size));
                    }
                }
            });
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        return new WriteBenchmarkResult
        {
            Label = label,
            Count = count,
            BatchSize = batchSize,
            TotalMs = sw.Elapsed.TotalMilliseconds,
            Latency = latencies is null ? LatencyStats.Empty : new LatencyStats([.. latencies]),
        };
    }

    public static void PrintWriteComparison(string title, int[] batchSizes, WriteBenchmarkResult[][] resultsByBatchSize)
    {
        const int labelWidth = 20;
        const int colWidth = 38;

        var repoLabels = resultsByBatchSize[0].Select(r => r.Label).ToArray();
        var totalWidth = labelWidth + colWidth * repoLabels.Length;

        Console.WriteLine();
        Console.WriteLine($"=== {title} ({resultsByBatchSize[0][0].Count:N0} ops) ===");
        Console.WriteLine();

        var header = "".PadRight(labelWidth) + string.Concat(repoLabels.Select(l => l.PadLeft(colWidth)));
        Console.WriteLine(header);
        Console.WriteLine(new string('=', totalWidth));

        Console.WriteLine("Throughput".PadRight(totalWidth, '-'));
        for (var i = 0; i < batchSizes.Length; i++)
        {
            PrintWriteRow($"  batch {batchSizes[i]:N0}", labelWidth, colWidth, resultsByBatchSize[i],
                r => $"{r.WritesPerSecond:N0} writes/sec");
        }

        var batchOneIndex = Array.IndexOf(batchSizes, 1);
        if (batchOneIndex >= 0)
        {
            Console.WriteLine();
            Console.WriteLine("Per-Op Latency (batch=1)".PadRight(totalWidth, '-'));
            var oneRes = resultsByBatchSize[batchOneIndex];
            PrintWriteRow("  p50", labelWidth, colWidth, oneRes, r => $"{r.Latency.P50Ms:F3} ms");
            PrintWriteRow("  p95", labelWidth, colWidth, oneRes, r => $"{r.Latency.P95Ms:F3} ms");
            PrintWriteRow("  p99", labelWidth, colWidth, oneRes, r => $"{r.Latency.P99Ms:F3} ms");
            PrintWriteRow("  p999", labelWidth, colWidth, oneRes, r => $"{r.Latency.P999Ms:F3} ms");
        }

        Console.WriteLine();
    }

    private static void PrintWriteRow(string name, int labelWidth, int colWidth,
        WriteBenchmarkResult[] results, Func<WriteBenchmarkResult, string> valueSelector)
    {
        var row = name.PadRight(labelWidth) + string.Concat(results.Select(r => valueSelector(r).PadLeft(colWidth)));
        Console.WriteLine(row);
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