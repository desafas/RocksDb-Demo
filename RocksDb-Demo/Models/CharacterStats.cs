using MemoryPack;
using MessagePack;

namespace RocksDb_Demo.Models;

[MemoryPackable]
[MessagePackObject]
public partial class CharacterStats
{
    [Key(0)] public int Strength { get; set; }
    [Key(1)] public int Dexterity { get; set; }
    [Key(2)] public int Intelligence { get; set; }
    [Key(3)] public int Vitality { get; set; }
    [Key(4)] public int Agility { get; set; }
    [Key(5)] public int Luck { get; set; }
}