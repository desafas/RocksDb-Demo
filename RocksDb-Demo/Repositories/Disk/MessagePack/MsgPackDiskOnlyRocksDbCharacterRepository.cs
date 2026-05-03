using System.Buffers.Binary;
using MessagePack;
using RocksDb_Demo.Models;
using RocksDb_Demo.Storage;
using RocksDbSharp;

namespace RocksDb_Demo.Repositories.Disk.MessagePack;

internal class MsgPackDiskOnlyRocksDbCharacterRepository : ICharacterRepository, ICompactionMonitorable, ISettleable, IDisposable
{
    private readonly string _dbPath;
    private readonly ThreadLocal<byte[]> _keyBuffer = new(() => new byte[8]);
    private RocksDb _db = null!;

    public MsgPackDiskOnlyRocksDbCharacterRepository()
    {
        _dbPath = Path.Combine(AppContext.BaseDirectory, "rocksdb-msgpack-diskonly");
        OpenFresh();
        Console.WriteLine($"RocksDB (MsgPack - DiskOnly) ready at: {_dbPath}");
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
        _db.Settle();
    }

    public PlayerCharacter? GetCharacter(long id)
    {
        var key = _keyBuffer.Value!;
        BinaryPrimitives.WriteInt64BigEndian(key, id);
        var value = _db.Get(key);
        return value is null ? null : MessagePackSerializer.Deserialize<PlayerCharacter>(value);
    }

    public void GetCharacters(ReadOnlyMemory<long> ids)
    {
        var span = ids.Span;
        var keys = new byte[span.Length][];
        for (var i = 0; i < span.Length; i++)
        {
            var k = new byte[8];
            BinaryPrimitives.WriteInt64BigEndian(k, span[i]);
            keys[i] = k;
        }
        var results = _db.MultiGet(keys, null, null);
        foreach (var kv in results)
            if (kv.Value is not null)
                MessagePackSerializer.Deserialize<PlayerCharacter>(kv.Value);
    }

    public void UpdateCharacter(PlayerCharacter character)
    {
        var key = _keyBuffer.Value!;
        BinaryPrimitives.WriteInt64BigEndian(key, character.Id);
        _db.Put(key, MessagePackSerializer.Serialize(character));
    }

    public void WriteBatch(ReadOnlyMemory<PlayerCharacter> batch)
    {
        var span = batch.Span;
        var key = _keyBuffer.Value!;
        using var wb = new WriteBatch();
        foreach (var t in span)
        {
            BinaryPrimitives.WriteInt64BigEndian(key, t.Id);
            wb.Put(key, MessagePackSerializer.Serialize(t));
        }
        _db.Write(wb);
    }

    public void Settle() => _db.ForceSettle();

    public string? GetCfStats() => _db.GetProperty("rocksdb.cfstats");

    public void Truncate()
    {
        _db.Dispose();
        OpenFresh();
    }

    private void OpenFresh()
    {
        if (Directory.Exists(_dbPath))
            Directory.Delete(_dbPath, true);
        Directory.CreateDirectory(_dbPath);
        _db = RocksDb.Open(new RocksDbSettings { Mode = RocksDbMode.DiskOnly }.BuildDbOptions(), _dbPath);
    }

    public void Dispose()
    {
        _db.Dispose();
        _keyBuffer.Dispose();
    }
}
