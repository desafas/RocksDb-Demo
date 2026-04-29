using System.Buffers.Binary;
using MemoryPack;
using RocksDb_Demo.Models;
using RocksDb_Demo.Storage;
using RocksDbSharp;

namespace RocksDb_Demo.Repositories.Disk.MemoryPack;

internal class CachedRocksDbCharacterRepository : ICharacterRepository, ICompactionMonitorable, IDisposable
{
    private readonly RocksDbMode _mode;
    private readonly string _dbPath;
    private readonly ThreadLocal<byte[]> _keyBuffer = new(() => new byte[8]);
    private readonly string _label;
    private RocksDb _db = null!;

    public CachedRocksDbCharacterRepository(RocksDbMode mode)
    {
        _mode = mode;
        _label = mode == RocksDbMode.Cache2Gb
            ? "RocksDB (MemoryPack - Cache 2GB)"
            : "RocksDB (MemoryPack - Cache 512MB)";

        _dbPath = Path.Combine(AppContext.BaseDirectory,
            mode == RocksDbMode.Cache2Gb ? "rocksdb-cache-2gb" : "rocksdb-cache-512mb");
        OpenFresh();
        Console.WriteLine($"{_label} ready at: {_dbPath}");
    }

    public void Initialize(Dictionary<long, PlayerCharacter> characters)
    {
        using var batch = new WriteBatch();
        var key = new byte[8];

        foreach (var (id, character) in characters)
        {
            BinaryPrimitives.WriteInt64BigEndian(key, id);
            batch.Put(key, MemoryPackSerializer.Serialize(character));
        }

        _db.Write(batch);
        _db.Settle();
    }

    public PlayerCharacter? GetCharacter(long id)
    {
        var key = _keyBuffer.Value!;
        BinaryPrimitives.WriteInt64BigEndian(key, id);
        var value = _db.Get(key);
        return value is null ? null : MemoryPackSerializer.Deserialize<PlayerCharacter>(value);
    }

    public void UpdateCharacter(PlayerCharacter character)
    {
        var key = _keyBuffer.Value!;
        BinaryPrimitives.WriteInt64BigEndian(key, character.Id);
        _db.Put(key, MemoryPackSerializer.Serialize(character));
    }

    public void WriteBatch(ReadOnlyMemory<PlayerCharacter> batch)
    {
        var span = batch.Span;
        var key = _keyBuffer.Value!;
        using var wb = new WriteBatch();
        foreach (var t in span)
        {
            BinaryPrimitives.WriteInt64BigEndian(key, t.Id);
            wb.Put(key, MemoryPackSerializer.Serialize(t));
        }
        _db.Write(wb);
    }

    public void Truncate()
    {
        _db.Dispose();
        OpenFresh();
    }

    public bool IsFlushActive =>
        _db.GetProperty("rocksdb.num-running-flushes") is string s && s != "0";

    public string? GetCfStats() => _db.GetProperty("rocksdb.cfstats");

    private void OpenFresh()
    {
        if (Directory.Exists(_dbPath))
            Directory.Delete(_dbPath, true);
        Directory.CreateDirectory(_dbPath);
        _db = RocksDb.Open(new RocksDbSettings { Mode = _mode }.BuildDbOptions(), _dbPath);
    }

    public void Dispose()
    {
        _db.Dispose();
        _keyBuffer.Dispose();
    }
}
