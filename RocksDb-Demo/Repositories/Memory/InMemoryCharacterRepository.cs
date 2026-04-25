using System.Collections.Concurrent;
using RocksDb_Demo.Models;

namespace RocksDb_Demo.Repositories.Memory;

class InMemoryCharacterRepository : ICharacterRepository
{
    private readonly ConcurrentDictionary<long, PlayerCharacter> _characters = new();

    public void Initialize(Dictionary<long, PlayerCharacter> characters)
    {
        foreach (var (id, character) in characters)
            _characters[id] = character;
    }

    public PlayerCharacter? GetCharacter(long id) =>
        _characters.TryGetValue(id, out var character) ? character : null;
}
