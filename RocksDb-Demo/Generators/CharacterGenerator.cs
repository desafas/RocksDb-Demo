using Bogus;
using RocksDb_Demo.Models;

namespace RocksDb_Demo.Generators;

internal static class CharacterGenerator
{
    private static long _idCounter;

    private static readonly Faker<PlayerCharacter> _faker = new Faker<PlayerCharacter>()
        .RuleFor(c => c.Id, f => Interlocked.Increment(ref _idCounter) - 1)
        .RuleFor(c => c.Name, f => f.Internet.UserName())
        .RuleFor(c => c.GuildName, f => $"{f.Hacker.Adjective()} {f.Hacker.Noun()}")
        .RuleFor(c => c.Level, f => f.Random.Int(1, 60))
        .RuleFor(c => c.Experience, f => f.Random.Long(0, 10_000_000))
        .RuleFor(c => c.Gold, f => f.Random.Long(0, 1_000_000))
        .RuleFor(c => c.Class, f => f.PickRandom<CharacterClass>())
        .RuleFor(c => c.Race, f => f.PickRandom<Race>())
        .RuleFor(c => c.X, f => f.Random.Float(-5000, 5000))
        .RuleFor(c => c.Y, f => f.Random.Float(-5000, 5000))
        .RuleFor(c => c.Z, f => f.Random.Float(0, 500))
        .RuleFor(c => c.MapId, f => f.Random.Int(1, 100))
        .RuleFor(c => c.LastLogin, f => f.Date.Recent(30).ToUniversalTime())
        .RuleFor(c => c.CreatedAt, f => f.Date.Past(8).ToUniversalTime())
        .RuleFor(c => c.Stats, f => new CharacterStats
        {
            Strength = f.Random.Int(1, 100),
            Dexterity = f.Random.Int(1, 100),
            Intelligence = f.Random.Int(1, 100),
            Vitality = f.Random.Int(1, 100),
            Agility = f.Random.Int(1, 100),
            Luck = f.Random.Int(1, 100)
        })
        .RuleFor(c => c.Inventory, f => new Inventory
        {
            Capacity = f.Random.Int(40, 60),
            Weight = f.Random.Float(0, 200),
            ItemIds = Enumerable.Range(0, 50).Select(_ => f.Random.Long(1, 100_000)).ToArray()
        })
        .RuleFor(c => c.Equipment, f => new Equipment
        {
            Head = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Shoulders = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Back = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Chest = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Waist = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Legs = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Feet = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Hands = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Neck = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Ring1 = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Ring2 = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Trinket1 = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Trinket2 = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            MainHand = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            OffHand = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000),
            Ranged = f.Random.Bool() ? 0L : f.Random.Long(1, 50_000)
        })
        .RuleFor(c => c.KnownSkills, f => Enumerable.Range(0, 30).Select(_ => f.Random.Int(1, 5_000)).ToArray())
        .RuleFor(c => c.AchievementFlags, f => f.Random.Bytes(64));

    public static PlayerCharacter[] GenerateCharacters(int count)
    {
        var characters = new PlayerCharacter[count];

        for (var i = 0; i < count; i++)
        {
            characters[i] = _faker.Generate();

            if ((i + 1) % 100_000 == 0)
                Console.WriteLine($"  {i + 1:N0} / {count:N0}");
        }

        return characters;
    }
}