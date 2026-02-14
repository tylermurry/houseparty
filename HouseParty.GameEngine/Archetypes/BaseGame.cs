using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Archetypes;

public sealed record StartGameResult(string GameId, List<GameEvent> Events);

public interface IBaseGame
{
    Task<StartGameResult> StartGame(string playerId, long now);
    Task<List<GameEvent>> StopGame(OperationContext context);
}

public class BaseGame(IExclusiveOperations exclusiveOperations, IPrimitives primitives) : IBaseGame
{
    public const string AdminRoleId = "admin-role";
    protected IExclusiveOperations ExclusiveOperations { get; } = exclusiveOperations;

    public async Task<StartGameResult> StartGame(string playerId, long now)
    {
        var gameId = Guid.NewGuid().ToString("n");
        var context = new OperationContext(gameId, playerId, now);

        var claimRoleResult = await ExclusiveOperations.ClaimRole(context, AdminRoleId);

        if (!claimRoleResult.Succeeded)
            throw new Exception("Game already started");

        return new StartGameResult(gameId, [..claimRoleResult.Events]);
    }

    protected async Task<bool> IsGameStarted(OperationContext context)
    {
        var adminRoleHolder = await primitives.GetTokenHolderAsync(context.GameId, AdminRoleId);
        return !string.IsNullOrWhiteSpace(adminRoleHolder);
    }

    public async Task<List<GameEvent>> StopGame(OperationContext context)
    {
        var adminRoleHolder = await primitives.GetTokenHolderAsync(context.GameId, AdminRoleId);

        if (string.IsNullOrWhiteSpace(adminRoleHolder))
            throw new Exception("Game not started");

        if (!string.Equals(adminRoleHolder, context.PlayerId, StringComparison.Ordinal))
            throw new Exception("Only admin can stop game");

        await primitives.ClearGameAsync(context.GameId);

        return [];
    }
}
