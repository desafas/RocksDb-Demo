using RocksDb_Demo.Models;

namespace RocksDb_Demo.Repositories;

internal interface ICharacterRepository
{
    void Initialize(Dictionary<long, PlayerCharacter> characters);
    PlayerCharacter? GetCharacter(long id);
    void GetCharacters(ReadOnlyMemory<long> ids);
    void UpdateCharacter(PlayerCharacter character);
    void WriteBatch(ReadOnlyMemory<PlayerCharacter> batch);
    void Truncate();
}
