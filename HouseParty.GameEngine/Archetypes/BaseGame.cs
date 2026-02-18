using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;

namespace HouseParty.GameEngine.Archetypes;

public sealed record CreateGameResult(string GameId, List<GameEvent> Events);

public interface IBaseGame
{
    Task<CreateGameResult> CreateGame(string playerId, int seatCount, long now);
    Task<List<GameEvent>> JoinGame(OperationContext context);
    Task<List<GameEvent>> StartGame(OperationContext context);
    Task<List<GameEvent>> EndGame(OperationContext context);
}

public class BaseGame(
    IExclusiveOperations exclusiveOperations,
    IGameOperations gameOperations,
    IPolicies policies
) : IBaseGame
{
    private const string SeatTokenPrefix = "seat:";

    public async Task<CreateGameResult> CreateGame(string playerId, int seatCount, long now)
    {
        if (seatCount <= 0)
            throw new Exception("Seat count must be greater than zero");

        var gameId = Guid.NewGuid().ToString("n");
        var context = new OperationContext(gameId, playerId, now);

        var claimRoleResult = await exclusiveOperations.ClaimRole(context, Policies.AdminRoleId);
        if (!claimRoleResult.Succeeded)
            throw new Exception("Failed to assign admin role");

        if (await gameOperations.GetMetadata(context) is not null)
            throw new Exception("Game already exists");

        await gameOperations.SaveMetadata(context, new GameMetadata(
            Status: "created",
            TotalSeats: seatCount,
            SeatedPlayerIds: [],
            AdminPlayerId: playerId
        ));

        return new CreateGameResult(gameId,
        [
            .. claimRoleResult.Events,
            new GameCreatedEvent(gameId)
            {
                PlayerId = playerId,
                Timestamp = now
            }
        ]);
    }

    public async Task<List<GameEvent>> JoinGame(OperationContext context)
    {
        var metadata = await gameOperations.GetMetadata(context);

        if (!policies.IsGameCreated(metadata))
            throw new Exception("Game not created");

        if (policies.IsGameStarted(metadata))
            throw new Exception("Game already started");

        if (policies.IsPlayerSeated(metadata, context.PlayerId))
            throw new Exception("Player already joined");

        if (policies.AreAllSeatsOccupied(metadata))
            throw new Exception("No available seats");

        var seatTokenId = $"{SeatTokenPrefix}{metadata!.SeatedPlayerIds.Count + 1}";
        var claimSeatResult = await exclusiveOperations.ControlObject(context, seatTokenId);

        if (!claimSeatResult.Succeeded)
            throw new Exception("No available seats");

        await gameOperations.SaveMetadata(context, metadata with
        {
            SeatedPlayerIds = [.. metadata.SeatedPlayerIds, context.PlayerId]
        });

        return
        [
            .. claimSeatResult.Events,
            new PlayerJoinedGameEvent
            {
                PlayerId = context.PlayerId,
                Timestamp = context.Now
            }
        ];
    }

    public async Task<List<GameEvent>> StartGame(OperationContext context)
    {
        var metadata = await gameOperations.GetMetadata(context);

        if (!policies.IsGameCreated(metadata))
            throw new Exception("Game not created");

        if (policies.IsGameStarted(metadata))
            throw new Exception("Game already started");

        if (!policies.IsPlayerSeated(metadata, context.PlayerId))
            throw new Exception("Only seated players can perform this action");

        if (!policies.AreAllSeatsOccupied(metadata))
            throw new Exception("All seats must be occupied before starting");

        await gameOperations.SaveMetadata(context, metadata! with { Status = "started" });

        return
        [
            new GameStartedEvent
            {
                PlayerId = context.PlayerId,
                Timestamp = context.Now
            }
        ];
    }

    public async Task<List<GameEvent>> EndGame(OperationContext context)
    {
        var metadata = await gameOperations.GetMetadata(context);

        if (!policies.IsGameStarted(metadata))
            throw new Exception("Game not started");

        if (!policies.IsPlayerAdminRole(metadata, context.PlayerId))
            throw new Exception("Only admin can stop game");

        if (! await gameOperations.ClearGame(context))
            throw new Exception("Failed to clear game data");

        return [new GameEndedEvent
        {
            PlayerId = context.PlayerId,
            Timestamp = context.Now
        }];
    }
}
