using MemoryPack;
using MessagePack;

namespace RocksDb_Demo.Models;

[MemoryPackable]
[MessagePackObject]
public partial class Inventory
{
    [Key(0)] public int Capacity { get; set; }
    [Key(1)] public float Weight { get; set; }
    [Key(2)] public long[] ItemIds { get; set; } = [];
}