using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;

namespace HouseParty.GameEngine.Archetypes;

public interface ITurnBasedGame : IBaseGame
{
    Task<List<GameEvent>> StartTurn(OperationContext context);
    Task<List<GameEvent>> MakeMove(OperationContext context, string move);
}

public class TurnBasedGame(
    IExclusiveOperations exclusiveOperations,
    IContestedOperations contestedOperations,
    IGameOperations gameOperations,
    IPolicies policies)
    : BaseGame(exclusiveOperations, gameOperations, policies), ITurnBasedGame
{
    private readonly IPolicies _policies = policies;
    private readonly IExclusiveOperations _exclusiveOperations = exclusiveOperations;

    private const string TurnTokenId = "turn";

    public async Task<List<GameEvent>> StartTurn(OperationContext context)
    {
        if (!await _policies.IsGameStarted(context.GameId))
            throw new Exception("Game not started");

        var controlTurnResult = await _exclusiveOperations.ControlObject(context, TurnTokenId);

        if (!controlTurnResult.Succeeded)
            throw new Exception("Turn already started");

        var activePlayerResult = await _exclusiveOperations.ControlObject(context, Policies.ActivePlayerTokenId);

        if (!activePlayerResult.Succeeded)
        {
            var revokeResult = await _exclusiveOperations.RevokeObjectControl(context, TurnTokenId);

            if (!revokeResult.Succeeded)
                throw new Exception("Could not revoke turn token");

            throw new Exception("Could not set active player");
        }

        return [..controlTurnResult.Events, ..activePlayerResult.Events];
    }

    public async Task<List<GameEvent>> MakeMove(OperationContext context, string move)
    {
        if (!await _policies.IsGameStarted(context.GameId))
            throw new Exception("Game not started");

        if (!await _policies.IsActivePlayer(context.GameId, context.PlayerId))
            throw new Exception("Only active player can make a move");

        if (string.IsNullOrWhiteSpace(move))
            throw new Exception("Move is required");

        // We are guaranteeing that only the active player can make a move,
        // so even though making a turn-based move is not a "contested" operation,
        // the SubmitAction call is exactly what we need.
        var makeMoveEvent = await contestedOperations.SubmitAction(context, move);

        return [..makeMoveEvent.Events];
    }
}
