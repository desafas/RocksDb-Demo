namespace RocksDb_Demo.Repositories;

internal interface ICompactionMonitorable
{
    string? GetCfStats();
}
