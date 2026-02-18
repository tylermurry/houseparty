using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine;

public interface IPolicies
{
    bool IsGameCreated(GameMetadata? metadata);
    bool IsGameStarted(GameMetadata? metadata);
    bool IsPlayerSeated(GameMetadata? metadata, string playerId);
    bool AreAllSeatsOccupied(GameMetadata? metadata);
    bool IsPlayerAdminRole(GameMetadata? metadata, string playerId);
    Task<bool> IsActivePlayer(string gameId, string playerId);
    Task<bool> IsTurnActive(string gameId);
}

public class Policies(IPrimitives primitives) : IPolicies
{
    public const string AdminRoleId = "admin-role";
    public const string ActivePlayerTokenId = "active-player";
    public const string TurnTokenId = "turn";

    public bool IsGameCreated(GameMetadata? metadata) => metadata is not null;

    public bool IsGameStarted(GameMetadata? metadata) => IsGameCreated(metadata) && string.Equals(metadata!.Status, "started", StringComparison.OrdinalIgnoreCase);

    public bool IsPlayerSeated(GameMetadata? metadata, string playerId)
    {
        if (metadata is null)
            return false;

        return metadata.SeatedPlayerIds.Any(id => string.Equals(id, playerId, StringComparison.Ordinal));
    }

    public bool AreAllSeatsOccupied(GameMetadata? metadata)
    {
        if (metadata is null || metadata.TotalSeats <= 0)
            return false;

        return metadata.SeatedPlayerIds.Distinct(StringComparer.Ordinal).Count() >= metadata.TotalSeats;
    }

    public bool IsPlayerAdminRole(GameMetadata? metadata, string playerId) =>
        metadata is not null && string.Equals(metadata.AdminPlayerId, playerId, StringComparison.Ordinal);

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
