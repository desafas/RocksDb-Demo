using System.Collections.Concurrent;
using RocksDb_Demo.Models;

namespace RocksDb_Demo.Repositories.Memory;

internal class InMemoryCharacterRepository : ICharacterRepository
{
    private readonly ConcurrentDictionary<long, PlayerCharacter> _characters = new();

    public void Initialize(Dictionary<long, PlayerCharacter> characters)
    {
        foreach (var (id, character) in characters)
            _characters[id] = character;
    }

    public PlayerCharacter? GetCharacter(long id)
    {
        return _characters.TryGetValue(id, out var character) ? character : null;
    }

    public void GetCharacters(ReadOnlyMemory<long> ids)
    {
        var span = ids.Span;
        for (var i = 0; i < span.Length; i++)
            GetCharacter(span[i]);
    }

    public void UpdateCharacter(PlayerCharacter character)
    {
        _characters[character.Id] = character;
    }

    public void WriteBatch(ReadOnlyMemory<PlayerCharacter> batch)
    {
        var span = batch.Span;
        for (var i = 0; i < span.Length; i++)
            _characters[span[i].Id] = span[i];
    }

    public void Truncate()
    {
        _characters.Clear();
    }
}
