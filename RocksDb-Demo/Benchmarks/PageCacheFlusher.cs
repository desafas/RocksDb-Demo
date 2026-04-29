namespace RocksDb_Demo.Benchmarks;

internal static class PageCacheFlusher
{
    private static bool? _available;

    public static void Flush()
    {
        if (_available == false)
            return;

        try
        {
            File.WriteAllText("/proc/sys/vm/drop_caches", "3");
            _available = true;
        }
        catch (Exception ex) when (_available is null)
        {
            _available = false;
            Console.WriteLine(
                $"  WARNING: page cache flush failed ({ex.Message}). " +
                "Run the container with --cap-add SYS_ADMIN.");
        }
    }
}
