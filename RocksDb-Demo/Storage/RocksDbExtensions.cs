using RocksDbSharp;

namespace RocksDb_Demo.Storage;

internal static class RocksDbExtensions
{
    public static bool SettleEnabled { get; set; }

    public static void Settle(this RocksDb db)
    {
        if (!SettleEnabled)
            return;

        db.ForceSettle();
    }

    public static void ForceSettle(this RocksDb db)
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
