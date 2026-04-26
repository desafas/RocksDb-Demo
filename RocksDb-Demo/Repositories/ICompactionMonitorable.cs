namespace RocksDb_Demo.Repositories;

internal interface ICompactionMonitorable
{
    bool IsFlushActive { get; }
    string? GetCfStats();
}
