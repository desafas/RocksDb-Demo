using Microsoft.Extensions.DependencyInjection;
using RocksDb_Demo.Generators;
using RocksDb_Demo.Models;
using RocksDb_Demo.Repositories;
using RocksDb_Demo.Repositories.Disk.MemoryPack;
using RocksDb_Demo.Repositories.Disk.MessagePack;
using RocksDb_Demo.Repositories.Memory;
using RocksDb_Demo.Storage;

namespace RocksDb_Demo.Benchmarks;

internal static class ServiceCollectionExtensions
{
    public const string LabelInMemory      = "InMemory";
    public const string LabelMsgPackDisk   = "MsgPack-Disk";
    public const string LabelMemPackDisk   = "MemPack-Disk";
    public const string LabelMsgPack512Mb  = "MsgPack-512MB";
    public const string LabelMemPack512Mb  = "MemPack-512MB";
    public const string LabelMsgPack2Gb    = "MsgPack-2GB";
    public const string LabelMemPack2Gb    = "MemPack-2GB";

    public static IServiceCollection AddCharacterRepositories(this IServiceCollection services)
    {
        return services
            .AddKeyedSingleton<ICharacterRepository, InMemoryCharacterRepository>("inmemory")
            .AddKeyedSingleton<ICharacterRepository, MsgPackDiskOnlyRocksDbCharacterRepository>("rocksdb-msgpack-diskonly")
            .AddKeyedSingleton<ICharacterRepository, DiskOnlyRocksDbCharacterRepository>("rocksdb-diskonly")
            .AddKeyedSingleton<ICharacterRepository>(
                "rocksdb-msgpack-cache-512mb",
                (_, _) => new MsgPackCachedRocksDbCharacterRepository(RocksDbMode.Cache512Mb))
            .AddKeyedSingleton<ICharacterRepository>(
                "rocksdb-mempack-cache-512mb",
                (_, _) => new CachedRocksDbCharacterRepository(RocksDbMode.Cache512Mb))
            .AddKeyedSingleton<ICharacterRepository>(
                "rocksdb-msgpack-cache-2gb",
                (_, _) => new MsgPackCachedRocksDbCharacterRepository(RocksDbMode.Cache2Gb))
            .AddKeyedSingleton<ICharacterRepository>(
                "rocksdb-mempack-cache-2gb",
                (_, _) => new CachedRocksDbCharacterRepository(RocksDbMode.Cache2Gb));
    }

    public static (ICharacterRepository[] AllRepos, string[] Labels, ICharacterRepository[] WarmableRepos)
        GetCharacterRepositories(this IServiceProvider provider)
    {
        var inMemory        = provider.GetRequiredKeyedService<ICharacterRepository>("inmemory");
        var msgPackDisk     = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-diskonly");
        var memPackDisk     = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-diskonly");
        var msgPack512Mb    = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-cache-512mb");
        var memPack512Mb    = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-mempack-cache-512mb");
        var msgPack2Gb      = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-cache-2gb");
        var memPack2Gb      = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-mempack-cache-2gb");

        ICharacterRepository[] allRepos =
            [inMemory, msgPackDisk, memPackDisk, msgPack512Mb, memPack512Mb, msgPack2Gb, memPack2Gb];
        string[] labels =
            [LabelInMemory, LabelMsgPackDisk, LabelMemPackDisk, LabelMsgPack512Mb, LabelMemPack512Mb, LabelMsgPack2Gb, LabelMemPack2Gb];
        ICharacterRepository[] warmableRepos =
            [msgPack512Mb, memPack512Mb, msgPack2Gb, memPack2Gb];

        return (allRepos, labels, warmableRepos);
    }

    public static (ICharacterRepository[] Repos, string[] Labels) GetCompactionBenchmarkRepos(
        this IServiceProvider provider)
    {
        var msgPackDisk  = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-diskonly");
        var memPackDisk  = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-diskonly");
        var msgPack2Gb   = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-cache-2gb");
        var memPack2Gb   = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-mempack-cache-2gb");
        return (
            [msgPackDisk, memPackDisk, msgPack2Gb, memPack2Gb],
            ["MsgPack (4MB)", "MemPack (4MB)", "MsgPack (128MB)", "MemPack (128MB)"]
        );
    }

    public static (ICharacterRepository[] Repos, string[] Labels) GetWriteBenchmarkRepos(
        this IServiceProvider provider)
    {
        var msgPackDisk  = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-diskonly");
        var memPackDisk  = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-diskonly");
        var msgPack2Gb   = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-cache-2gb");
        var memPack2Gb   = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-mempack-cache-2gb");
        return (
            [msgPackDisk, memPackDisk, msgPack2Gb, memPack2Gb],
            ["MsgPack (4MB)", "MemPack (4MB)", "MsgPack (128MB)", "MemPack (128MB)"]
        );
    }

    public static (PlayerCharacter[] WritePool, long Count) GenerateAndInitialize(this ICharacterRepository[] repos)
    {
        const int count = 1_000_000;
        Console.WriteLine($"Generating {count:N0} characters...");
        var writePool = CharacterGenerator.GenerateCharacters(count);
        var charactersById = writePool.ToDictionary(c => c.Id);
        var memoryUsed = GC.GetTotalMemory(true);
        Console.WriteLine();
        Console.WriteLine($"Done. {charactersById.Count:N0} characters loaded.");
        Console.WriteLine(
            $"Memory used   : {memoryUsed / 1024.0 / 1024.0:F1} MB  ({memoryUsed / 1024.0 / 1024.0 / 1024.0:F3} GB)");
        Console.WriteLine();

        foreach (var repo in repos)
        {
            repo.Initialize(charactersById);
            (repo as ISettleable)?.Settle();
        }
        Console.WriteLine("Repositories ready.");
        Console.WriteLine();

        return (writePool, charactersById.Count);
    }
}
