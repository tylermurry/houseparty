using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;

namespace HouseParty.GameEngine.Archetypes;

public interface ITurnBasedGame : IBaseGame
{
    Task<List<GameEvent>> StartTurn(OperationContext context);
    Task<(List<GameEvent> Events, string StatePayload)> EndTurn(OperationContext context, string statePayload);
    Task<List<GameEvent>> MakeMove(OperationContext context, string move);
}

public class TurnBasedGame(
    IExclusiveOperations exclusiveOperations,
    IContestedOperations contestedOperations,
    IGameOperations gameOperations,
    ICommitOperations commitOperations,
    IPolicies policies)
    : BaseGame(exclusiveOperations, gameOperations, policies), ITurnBasedGame
{
    private readonly IPolicies _policies = policies;
    private readonly IExclusiveOperations _exclusiveOperations = exclusiveOperations;
    private readonly IGameOperations _gameOperations = gameOperations;

    public async Task<List<GameEvent>> StartTurn(OperationContext context)
    {
        var metadata = await _gameOperations.GetMetadata(context);

        if (!_policies.IsGameStarted(metadata))
            throw new Exception("Game not started");

        if (!_policies.IsPlayerSeated(metadata, context.PlayerId))
            throw new Exception("Only seated players can perform this action");

        var controlTurnResult = await _exclusiveOperations.ControlObject(context, Policies.TurnTokenId);

        if (!controlTurnResult.Succeeded)
            throw new Exception("Turn already started");

        var activePlayerResult = await _exclusiveOperations.ControlObject(context, Policies.ActivePlayerTokenId);

        if (!activePlayerResult.Succeeded)
        {
            var revokeResult = await _exclusiveOperations.RevokeObjectControl(context, Policies.TurnTokenId);

            if (!revokeResult.Succeeded)
                throw new Exception("Could not revoke turn token");

            throw new Exception("Could not set active player");
        }

        return [.. controlTurnResult.Events, .. activePlayerResult.Events];
    }

    public async Task<(List<GameEvent> Events, string StatePayload)> EndTurn(OperationContext context, string statePayload)
    {
        var metadata = await _gameOperations.GetMetadata(context);

        if (!_policies.IsGameStarted(metadata))
            throw new Exception("Game not started");

        if (!_policies.IsPlayerSeated(metadata, context.PlayerId))
            throw new Exception("Only seated players can perform this action");

        if (!await _policies.IsTurnActive(context.GameId))
            throw new Exception("No active turn");

        if (!await _policies.IsActivePlayer(context.GameId, context.PlayerId))
            throw new Exception("Only active player can end turn");

        if (string.IsNullOrWhiteSpace(statePayload))
            throw new Exception("State payload is required");

        var releaseActivePlayerResult = await _exclusiveOperations.ReleaseObjectControl(context, Policies.ActivePlayerTokenId);

        if (!releaseActivePlayerResult.Succeeded)
            throw new Exception("Failed to release active player");

        var releaseTurnResult = await _exclusiveOperations.ReleaseObjectControl(context, Policies.TurnTokenId);

        if (!releaseTurnResult.Succeeded)
            throw new Exception("Failed to release turn");

        var savedState = await commitOperations.SaveData(context, statePayload);
        var eventsCleared = await _gameOperations.ClearEvents(context);

        if (!eventsCleared)
            throw new Exception("Failed to clear events");

        return ([.. releaseActivePlayerResult.Events, .. releaseTurnResult.Events], savedState.Data);
    }

    public async Task<List<GameEvent>> MakeMove(OperationContext context, string move)
    {
        var metadata = await _gameOperations.GetMetadata(context);

        if (!_policies.IsGameStarted(metadata))
            throw new Exception("Game not started");

        if (!_policies.IsPlayerSeated(metadata, context.PlayerId))
            throw new Exception("Only seated players can perform this action");

        if (!await _policies.IsActivePlayer(context.GameId, context.PlayerId))
            throw new Exception("Only active player can make a move");

        if (string.IsNullOrWhiteSpace(move))
            throw new Exception("Move is required");

        // We are guaranteeing that only the active player can make a move,
        // so even though making a turn-based move is not a "contested" operation,
        // the SubmitAction call is exactly what we need.
        var makeMoveEvent = await contestedOperations.SubmitAction(context, move);

        return [.. makeMoveEvent.Events];
    }
}
