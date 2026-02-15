using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;

namespace HouseParty.GameEngine.Archetypes;

public sealed record StartGameResult(string GameId, List<GameEvent> Events);

public interface IBaseGame
{
    Task<StartGameResult> StartGame(string playerId, long now);
    Task<List<GameEvent>> StopGame(OperationContext context);
}

public class BaseGame(
    IExclusiveOperations exclusiveOperations,
    IGameOperations gameOperations,
    IPolicies policies
) : IBaseGame
{
    public async Task<StartGameResult> StartGame(string playerId, long now)
    {
        var gameId = Guid.NewGuid().ToString("n");
        var context = new OperationContext(gameId, playerId, now);

        var claimRoleResult = await exclusiveOperations.ClaimRole(context, Policies.AdminRoleId);

        if (!claimRoleResult.Succeeded)
            throw new Exception("Game already started");

        return new StartGameResult(gameId, [..claimRoleResult.Events]);
    }

    public async Task<List<GameEvent>> StopGame(OperationContext context)
    {
        if (! await policies.IsGameStarted(context.GameId))
            throw new Exception("Game not started");

        if (! await policies.IsPlayerAdminRole(context.GameId, context.PlayerId))
            throw new Exception("Only admin can stop game");

        if (! await gameOperations.ClearGame(context))
            throw new Exception("Failed to clear game data");

        return [];
    }
}
