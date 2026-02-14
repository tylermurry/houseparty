using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;

namespace HouseParty.GameEngine.Archetypes;

public interface IBaseGame
{
    Task<List<GameEvent>> StartGame(OperationContext context);
}

public class BaseGame(IExclusiveOperations exclusiveOperations) : IBaseGame
{
    private const string AdminRoleId = "admin-role";

    public async Task<List<GameEvent>> StartGame(OperationContext context)
    {
        var claimRoleResult = await exclusiveOperations.ClaimRole(context, AdminRoleId);

        if (!claimRoleResult.Succeeded)
            throw new Exception("Game already started");

        return [..claimRoleResult.Events];
    }
}
