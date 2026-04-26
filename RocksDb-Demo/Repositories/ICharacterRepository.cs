using RocksDb_Demo.Models;

namespace RocksDb_Demo.Repositories;

internal interface ICharacterRepository
{
    void Initialize(Dictionary<long, PlayerCharacter> characters);
    PlayerCharacter? GetCharacter(long id);
    void UpdateCharacter(PlayerCharacter character);
}