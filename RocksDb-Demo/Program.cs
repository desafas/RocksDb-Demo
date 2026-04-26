using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RocksDb_Demo.Benchmarks;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine();

await using var provider = new ServiceCollection()
    .AddCharacterRepositories()
    .BuildServiceProvider();

var (allRepos, labels, warmableRepos) = provider.GetCharacterRepositories();
var (writePool, count) = allRepos.GenerateAndInitialize();

int[] threadCounts = [4, 16, 32, 64];
const int ReaderCount = 4;
const int WriterCount = 4;

var sequential = BenchmarkSuiteExtensions.RunSequential(allRepos, labels, count, warmableRepos);
var random = BenchmarkSuiteExtensions.RunRandom(allRepos, labels, count, warmableRepos);
var concurrent = await BenchmarkSuiteExtensions.RunConcurrent(allRepos, labels, count, warmableRepos, threadCounts);
var mixed = await BenchmarkSuiteExtensions.RunMixed(allRepos, labels, count, warmableRepos, writePool, ReaderCount,
    WriterCount);

BenchmarkRunner.PrintComparison("Sequential Read Benchmark", sequential);
BenchmarkRunner.PrintComparison("Random Read Benchmark", random);
BenchmarkRunner.PrintConcurrentComparison("Concurrent Random Read Benchmark", threadCounts, concurrent);
BenchmarkRunner.PrintComparison($"Mixed Read/Write Benchmark ({ReaderCount} readers, {WriterCount} writers)", mixed);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();