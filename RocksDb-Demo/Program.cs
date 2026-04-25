using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RocksDb_Demo.Benchmarks;
using RocksDb_Demo.Generators;
using RocksDb_Demo.Repositories;
using RocksDb_Demo.Repositories.Disk.MessagePack;
using RocksDb_Demo.Repositories.Disk.MemoryPack;
using RocksDb_Demo.Repositories.Memory;
using RocksDb_Demo.Storage;

Console.OutputEncoding = Encoding.UTF8;

using var provider = new ServiceCollection()
    .AddKeyedSingleton<ICharacterRepository, InMemoryCharacterRepository>("inmemory")
    .AddKeyedSingleton<ICharacterRepository, MsgPackDiskOnlyRocksDbCharacterRepository>("rocksdb-msgpack-diskonly")
    .AddKeyedSingleton<ICharacterRepository, DiskOnlyRocksDbCharacterRepository>("rocksdb-diskonly")
    .AddKeyedSingleton<ICharacterRepository>(
        "rocksdb-cache-512mb",
        (_, _) => new CachedRocksDbCharacterRepository(RocksDbMode.Cache512Mb))
    .AddKeyedSingleton<ICharacterRepository>(
        "rocksdb-cache-2gb",
        (_, _) => new CachedRocksDbCharacterRepository(RocksDbMode.Cache2Gb))
    .BuildServiceProvider();

var inMemoryRepo        = provider.GetRequiredKeyedService<ICharacterRepository>("inmemory");
var msgPackDiskOnlyRepo = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-diskonly");
var diskOnlyRepo        = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-diskonly");
var cache512MbRepo      = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-cache-512mb");
var cache2GbRepo        = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-cache-2gb");

const string LabelInMemory     = "InMemory";
const string LabelMsgPack      = "RocksDB (MsgPack - DiskOnly)";
const string LabelMemPack      = "RocksDB (MemoryPack - DiskOnly)";
const string LabelCache512Mb   = "RocksDB (MemoryPack - Cache 512MB)";
const string LabelCache2Gb     = "RocksDB (MemoryPack - Cache 2GB)";

ICharacterRepository[] allRepos   = [inMemoryRepo, msgPackDiskOnlyRepo, diskOnlyRepo, cache512MbRepo, cache2GbRepo];
string[]               repoLabels = [LabelInMemory, LabelMsgPack, LabelMemPack, LabelCache512Mb, LabelCache2Gb];
Console.WriteLine();

Console.WriteLine("Generating 1,000,000 characters...");
var characters = CharacterGenerator.GenerateOneMillionCharacters();
long memoryUsed = GC.GetTotalMemory(forceFullCollection: true);
Console.WriteLine();
Console.WriteLine($"Done. {characters.Count:N0} characters loaded.");
Console.WriteLine($"Memory used   : {memoryUsed / 1024.0 / 1024.0:F1} MB  ({memoryUsed / 1024.0 / 1024.0 / 1024.0:F3} GB)");
Console.WriteLine();

inMemoryRepo.Initialize(characters);
msgPackDiskOnlyRepo.Initialize(characters);
diskOnlyRepo.Initialize(characters);
cache512MbRepo.Initialize(characters);
cache2GbRepo.Initialize(characters);
Console.WriteLine("Repositories ready.");
Console.WriteLine();

// --- Sequential & random read benchmarks ---

Console.WriteLine("Running sequential benchmarks...");
var seqInMemory        = BenchmarkRunner.Run(inMemoryRepo,        characters.Count, LabelInMemory);
var seqMsgPackDiskOnly = BenchmarkRunner.Run(msgPackDiskOnlyRepo, characters.Count, LabelMsgPack);
var seqDiskOnly        = BenchmarkRunner.Run(diskOnlyRepo,        characters.Count, LabelMemPack);
BenchmarkRunner.Run(cache512MbRepo, characters.Count, LabelCache512Mb, isWarmup: true);
var seqCache512Mb      = BenchmarkRunner.Run(cache512MbRepo,      characters.Count, LabelCache512Mb);
BenchmarkRunner.Run(cache2GbRepo,   characters.Count, LabelCache2Gb,   isWarmup: true);
var seqCache2Gb        = BenchmarkRunner.Run(cache2GbRepo,        characters.Count, LabelCache2Gb);

Console.WriteLine("Running random benchmarks...");
var rndInMemory        = BenchmarkRunner.Run(inMemoryRepo,        characters.Count, LabelInMemory,   randomize: true);
var rndMsgPackDiskOnly = BenchmarkRunner.Run(msgPackDiskOnlyRepo, characters.Count, LabelMsgPack,    randomize: true);
var rndDiskOnly        = BenchmarkRunner.Run(diskOnlyRepo,        characters.Count, LabelMemPack,    randomize: true);
BenchmarkRunner.Run(cache512MbRepo, characters.Count, LabelCache512Mb, randomize: true, isWarmup: true);
var rndCache512Mb      = BenchmarkRunner.Run(cache512MbRepo,      characters.Count, LabelCache512Mb, randomize: true);
BenchmarkRunner.Run(cache2GbRepo,   characters.Count, LabelCache2Gb,   randomize: true, isWarmup: true);
var rndCache2Gb        = BenchmarkRunner.Run(cache2GbRepo,        characters.Count, LabelCache2Gb,   randomize: true);

// --- Concurrent random read benchmark ---

int[] threadCounts = [4, 16, 32, 64];

Console.WriteLine("Warming caches for concurrent benchmark...");
BenchmarkRunner.Run(cache512MbRepo, characters.Count, LabelCache512Mb, isWarmup: true);
BenchmarkRunner.Run(cache2GbRepo,   characters.Count, LabelCache2Gb,   isWarmup: true);

Console.WriteLine("Running concurrent random read benchmarks...");
var concurrentResults = threadCounts.Select(tc =>
    allRepos.Zip(repoLabels)
            .Select(p => BenchmarkRunner.RunConcurrent(p.First, characters.Count, tc, p.Second, randomize: true))
            .ToArray()
).ToArray();

// --- Print all results ---

BenchmarkRunner.PrintComparison("Sequential Read Benchmark",    seqInMemory, seqMsgPackDiskOnly, seqDiskOnly, seqCache512Mb, seqCache2Gb);
BenchmarkRunner.PrintComparison("Random Read Benchmark",        rndInMemory, rndMsgPackDiskOnly, rndDiskOnly, rndCache512Mb, rndCache2Gb);
BenchmarkRunner.PrintConcurrentComparison("Concurrent Random Read Benchmark", threadCounts, concurrentResults);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
