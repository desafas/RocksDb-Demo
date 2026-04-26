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
    public const string LabelInMemory = "InMemory";
    public const string LabelMsgPack = "RocksDB (MsgPack - DiskOnly)";
    public const string LabelMemPack = "RocksDB (MemoryPack - DiskOnly)";
    public const string LabelCache512Mb = "RocksDB (MemoryPack - Cache 512MB)";
    public const string LabelCache2Gb = "RocksDB (MemoryPack - Cache 2GB)";

    public static IServiceCollection AddCharacterRepositories(this IServiceCollection services)
    {
        return services
            .AddKeyedSingleton<ICharacterRepository, InMemoryCharacterRepository>("inmemory")
            .AddKeyedSingleton<ICharacterRepository, MsgPackDiskOnlyRocksDbCharacterRepository>(
                "rocksdb-msgpack-diskonly")
            .AddKeyedSingleton<ICharacterRepository, DiskOnlyRocksDbCharacterRepository>("rocksdb-diskonly")
            .AddKeyedSingleton<ICharacterRepository>(
                "rocksdb-cache-512mb",
                (_, _) => new CachedRocksDbCharacterRepository(RocksDbMode.Cache512Mb))
            .AddKeyedSingleton<ICharacterRepository>(
                "rocksdb-cache-2gb",
                (_, _) => new CachedRocksDbCharacterRepository(RocksDbMode.Cache2Gb));
    }

    public static (ICharacterRepository[] AllRepos, string[] Labels, ICharacterRepository[] WarmableRepos)
        GetCharacterRepositories(this IServiceProvider provider)
    {
        var inMemoryRepo = provider.GetRequiredKeyedService<ICharacterRepository>("inmemory");
        var msgPackDiskOnlyRepo = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-msgpack-diskonly");
        var diskOnlyRepo = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-diskonly");
        var cache512MbRepo = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-cache-512mb");
        var cache2GbRepo = provider.GetRequiredKeyedService<ICharacterRepository>("rocksdb-cache-2gb");

        ICharacterRepository[] allRepos =
            [inMemoryRepo, msgPackDiskOnlyRepo, diskOnlyRepo, cache512MbRepo, cache2GbRepo];
        string[] labels = [LabelInMemory, LabelMsgPack, LabelMemPack, LabelCache512Mb, LabelCache2Gb];
        ICharacterRepository[] warmableRepos = [cache512MbRepo, cache2GbRepo];

        return (allRepos, labels, warmableRepos);
    }

    public static (PlayerCharacter[] WritePool, long Count) GenerateAndInitialize(this ICharacterRepository[] repos)
    {
        Console.WriteLine("Generating 1,000,000 characters...");
        var characters = CharacterGenerator.GenerateOneMillionCharacters();
        var memoryUsed = GC.GetTotalMemory(true);
        Console.WriteLine();
        Console.WriteLine($"Done. {characters.Count:N0} characters loaded.");
        Console.WriteLine(
            $"Memory used   : {memoryUsed / 1024.0 / 1024.0:F1} MB  ({memoryUsed / 1024.0 / 1024.0 / 1024.0:F3} GB)");
        Console.WriteLine();

        foreach (var repo in repos)
            repo.Initialize(characters);
        Console.WriteLine("Repositories ready.");
        Console.WriteLine();

        return (characters.Values.ToArray(), characters.Count);
    }
}