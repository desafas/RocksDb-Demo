using MemoryPack;
using MessagePack;

namespace RocksDb_Demo.Models;

public enum CharacterClass { Warrior, Mage, Rogue, Paladin, Hunter, Warlock, Priest, Shaman }
public enum Race { Human, Elf, Dwarf, Orc, Undead, Troll, Gnome, Tauren }

[MemoryPackable]
[MessagePackObject]
public partial class PlayerCharacter
{
    [Key(0)]  public long           Id               { get; set; }
    [Key(1)]  public string         Name             { get; set; } = "";
    [Key(2)]  public string         GuildName        { get; set; } = "";
    [Key(3)]  public int            Level            { get; set; }
    [Key(4)]  public long           Experience       { get; set; }
    [Key(5)]  public long           Gold             { get; set; }
    [Key(6)]  public CharacterClass Class            { get; set; }
    [Key(7)]  public Race           Race             { get; set; }
    [Key(8)]  public float          X                { get; set; }
    [Key(9)]  public float          Y                { get; set; }
    [Key(10)] public float          Z                { get; set; }
    [Key(11)] public int            MapId            { get; set; }
    [Key(12)] public DateTime       LastLogin        { get; set; }
    [Key(13)] public DateTime       CreatedAt        { get; set; }
    [Key(14)] public CharacterStats Stats            { get; set; } = new();
    [Key(15)] public Inventory      Inventory        { get; set; } = new();
    [Key(16)] public Equipment      Equipment        { get; set; } = new();
    [Key(17)] public int[]          KnownSkills      { get; set; } = [];
    [Key(18)] public byte[]         AchievementFlags { get; set; } = [];
}
