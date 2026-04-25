using System.Buffers.Binary;
using MessagePack;
using RocksDb_Demo.Models;
using RocksDb_Demo.Storage;
using RocksDbSharp;

namespace RocksDb_Demo.Repositories.Disk.MessagePack;

class MsgPackDiskOnlyRocksDbCharacterRepository : ICharacterRepository, IDisposable
{
    private readonly RocksDb             _db;
    private readonly ThreadLocal<byte[]> _keyBuffer = new(() => new byte[8]);

    public MsgPackDiskOnlyRocksDbCharacterRepository()
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "rocksdb-msgpack-diskonly");
        if (Directory.Exists(dbPath))
            Directory.Delete(dbPath, recursive: true);
        Directory.CreateDirectory(dbPath);
        _db = RocksDb.Open(new RocksDbSettings { Mode = RocksDbMode.DiskOnly }.BuildDbOptions(), dbPath);
        Console.WriteLine($"RocksDB (MsgPack - DiskOnly) ready at: {dbPath}");
    }

    public void Initialize(Dictionary<long, PlayerCharacter> characters)
    {
        using var batch = new WriteBatch();
        var key = new byte[8];

        foreach (var (id, character) in characters)
        {
            BinaryPrimitives.WriteInt64BigEndian(key, id);
            batch.Put(key, MessagePackSerializer.Serialize(character));
        }

        _db.Write(batch);
    }

    public PlayerCharacter? GetCharacter(long id)
    {
        var key = _keyBuffer.Value!;
        BinaryPrimitives.WriteInt64BigEndian(key, id);
        var value = _db.Get(key);
        return value is null ? null : MessagePackSerializer.Deserialize<PlayerCharacter>(value);
    }

    public void Dispose()
    {
        _db.Dispose();
        _keyBuffer.Dispose();
    }
}
