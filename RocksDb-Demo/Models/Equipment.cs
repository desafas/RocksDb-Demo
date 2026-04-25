using MemoryPack;
using MessagePack;

namespace RocksDb_Demo.Models;

[MemoryPackable]
[MessagePackObject]
public partial class Equipment
{
    [Key(0)]  public long Head      { get; set; }
    [Key(1)]  public long Shoulders { get; set; }
    [Key(2)]  public long Back      { get; set; }
    [Key(3)]  public long Chest     { get; set; }
    [Key(4)]  public long Waist     { get; set; }
    [Key(5)]  public long Legs      { get; set; }
    [Key(6)]  public long Feet      { get; set; }
    [Key(7)]  public long Hands     { get; set; }
    [Key(8)]  public long Neck      { get; set; }
    [Key(9)]  public long Ring1     { get; set; }
    [Key(10)] public long Ring2     { get; set; }
    [Key(11)] public long Trinket1  { get; set; }
    [Key(12)] public long Trinket2  { get; set; }
    [Key(13)] public long MainHand  { get; set; }
    [Key(14)] public long OffHand   { get; set; }
    [Key(15)] public long Ranged    { get; set; }
}
