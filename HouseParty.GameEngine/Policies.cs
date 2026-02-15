using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine;

public interface IPolicies
{
    Task<bool> IsGameStarted(string gameId);
    Task<bool> IsPlayerAdminRole(string gameId, string playerId);
    Task<bool> IsActivePlayer(string gameId, string playerId);
    Task<bool> IsTurnActive(string gameId);
}

public class Policies(IPrimitives primitives) : IPolicies
{
    public const string AdminRoleId = "admin-role";
    public const string ActivePlayerTokenId = "active-player";
    public const string TurnTokenId = "turn";

    public async Task<bool> IsGameStarted(string gameId)
    {
        var adminRoleHolder = await primitives.GetTokenHolderAsync(gameId, AdminRoleId);
        return !string.IsNullOrWhiteSpace(adminRoleHolder);
    }

    public async Task<bool> IsPlayerAdminRole(string gameId, string playerId)
    {
        var holderId = await primitives.GetTokenHolderAsync(gameId, AdminRoleId);
        return string.Equals(holderId, playerId, StringComparison.Ordinal);
    }

    public async Task<bool> IsActivePlayer(string gameId, string playerId)
    {
        var holderId = await primitives.GetTokenHolderAsync(gameId, ActivePlayerTokenId);
        return string.Equals(holderId, playerId, StringComparison.Ordinal);
    }

    public async Task<bool> IsTurnActive(string gameId)
    {
        var holderId = await primitives.GetTokenHolderAsync(gameId, TurnTokenId);
        return !string.IsNullOrWhiteSpace(holderId);
    }
}
