using RocksDbSharp;

namespace RocksDb_Demo.Storage;

internal static class RocksDbExtensions
{
    // Forces the DB to a steady state: flushes the MemTable to L0, then runs a full compaction.
    // After this returns, no background flush or compaction work is pending — reads against the
    // DB measure the on-disk LSM, not transient post-write activity.
    public static void Settle(this RocksDb db)
    {
        var flushOptions = Native.Instance.rocksdb_flushoptions_create();
        try
        {
            Native.Instance.rocksdb_flushoptions_set_wait(flushOptions, true);
            Native.Instance.rocksdb_flush(db.Handle, flushOptions);
        }
        finally
        {
            Native.Instance.rocksdb_flushoptions_destroy(flushOptions);
        }
        db.CompactRange((byte[]?)null, (byte[]?)null);
    }
}
