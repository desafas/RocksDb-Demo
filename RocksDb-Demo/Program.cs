using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RocksDb_Demo.Benchmarks;
using RocksDb_Demo.Generators;
using RocksDb_Demo.Models;
using RocksDb_Demo.Storage;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine();

ThreadPool.GetMinThreads(out _, out var minIoThreads);
ThreadPool.SetMinThreads(64, minIoThreads);

Console.WriteLine("Benchmark mode:");
Console.WriteLine("  1. Steady-state: no settle, no flush — OS cache warms naturally, compactions may be in flight [default]");
Console.WriteLine("  2. Cold-isolated: settle + flush — DB fully compacted and OS cache cleared before each benchmark");
Console.Write("Enter choice (1/2): ");
var modeInput = Console.ReadLine()?.Trim();
var coldIsolated = modeInput == "2";
RocksDbExtensions.SettleEnabled = coldIsolated;
PageCacheFlusher.Enabled = coldIsolated;
Console.WriteLine(coldIsolated
    ? "Cold-isolated — each benchmark starts with a fully compacted DB and cold OS cache. Measures worst-case cold-disk performance."
    : "Steady-state — DB is not settled and OS cache is not flushed. Measures realistic production behavior.");
Console.WriteLine();

await using var provider = new ServiceCollection()
    .AddCharacterRepositories()
    .BuildServiceProvider();

var (allRepos, labels, warmableRepos) = provider.GetCharacterRepositories();
var (compactionRepos, compactionLabels) = provider.GetCompactionBenchmarkRepos();
var (writeRepos, writeLabels) = provider.GetWriteBenchmarkRepos();
var (writePool, count) = allRepos.GenerateAndInitialize();

int[] threadCounts = [4, 16, 32, 64];
const int ReaderCount = 4;
const int WriterCount = 4;
const int WriteThreadCount = 16;
const int BulkReadBatchSize = 5_000;
int[] batchSizes = [1, 1_000, 10_000];

var random = BenchmarkSuiteExtensions.RunRandom(allRepos, labels, count, warmableRepos);
var bulkRead = BenchmarkSuiteExtensions.RunBulkRead(allRepos, labels, count, warmableRepos, BulkReadBatchSize);
var concurrent = await BenchmarkSuiteExtensions.RunConcurrent(allRepos, labels, count, warmableRepos, threadCounts);
var mixed = await BenchmarkSuiteExtensions.RunMixed(allRepos, labels, count, warmableRepos, writePool, ReaderCount, WriterCount);
var compaction = await BenchmarkSuiteExtensions.RunCompactionLatency(compactionRepos, compactionLabels, writePool, ReaderCount, WriterCount);

// Build the write pools.
// Update pool: shuffled clone of the existing 1M characters — each existing key written exactly once.
var baseCharacters = writePool.ToDictionary(c => c.Id);
var updatePool = (PlayerCharacter[])writePool.Clone();
Random.Shared.Shuffle(updatePool);

// Insert pool: 1M new characters with continuing IDs (1M..2M), shuffled.
Console.WriteLine($"Generating {count:N0} additional characters for insert benchmark...");
var insertPool = CharacterGenerator.GenerateCharacters((int)count);
Random.Shared.Shuffle(insertPool);
Console.WriteLine();

var updateWrites = await BenchmarkSuiteExtensions.RunWriteUpdate(
    writeRepos, writeLabels, baseCharacters, updatePool, batchSizes, WriteThreadCount);
var insertWrites = await BenchmarkSuiteExtensions.RunWriteInsert(
    writeRepos, writeLabels, baseCharacters, insertPool, batchSizes, WriteThreadCount);

BenchmarkRunner.PrintComparison("Random Read Benchmark", random);
BenchmarkRunner.PrintComparison($"Bulk Read Benchmark (batch={BulkReadBatchSize:N0})", bulkRead);
BenchmarkRunner.PrintConcurrentComparison("Concurrent Random Read Benchmark", threadCounts, concurrent);
BenchmarkRunner.PrintComparison($"Mixed Read/Write Benchmark ({ReaderCount} readers, {WriterCount} writers)", mixed);
BenchmarkRunner.PrintCompactionLatencyComparison($"Compaction Latency Benchmark ({count:N0} reads · {count:N0} writes)", compaction);
BenchmarkRunner.PrintWriteComparison($"Update Write Benchmark ({WriteThreadCount} threads)", batchSizes, updateWrites);
BenchmarkRunner.PrintWriteComparison($"Insert Write Benchmark ({WriteThreadCount} threads)", batchSizes, insertWrites);

if (!Console.IsInputRedirected)
{
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
