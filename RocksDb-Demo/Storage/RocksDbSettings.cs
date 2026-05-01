using RocksDbSharp;

namespace RocksDb_Demo.Storage;

internal class RocksDbSettings
{
    public RocksDbMode Mode { get; init; }

    public DbOptions BuildDbOptions()
    {
        var dbOptions = new DbOptions().SetCreateIfMissing();

        switch (Mode)
        {
            case RocksDbMode.DiskOnly:
            {
                var tableOptions = new BlockBasedTableOptions().SetNoBlockCache(true);
                dbOptions
                    .SetWriteBufferSize(4 * 1024 * 1024) // 4MB — minimum MemTable
                    .SetMaxWriteBufferNumber(2)
                    .SetBlockBasedTableFactory(tableOptions);
                break;
            }
            case RocksDbMode.Cache512Mb:
            case RocksDbMode.Cache2Gb:
            {
                var cacheSize = Mode == RocksDbMode.Cache2Gb
                    ? 2UL * 1024 * 1024 * 1024
                    : 512UL * 1024 * 1024;
                var blockCache = Cache.CreateLru(cacheSize);
                var tableOptions = new BlockBasedTableOptions()
                    .SetBlockCache(blockCache)
                    .SetCacheIndexAndFilterBlocks(true)
                    .SetPinL0FilterAndIndexBlocksInCache(true);
                dbOptions
                    .SetWriteBufferSize(128 * 1024 * 1024) // 128MB MemTable
                    .SetMaxWriteBufferNumber(2)
                    .SetBlockBasedTableFactory(tableOptions);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unhandled RocksDbMode");
        }

        return dbOptions;
    }
}