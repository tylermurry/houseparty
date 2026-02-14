using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Archetypes;

public interface ITurnBasedGame : IBaseGame
{
    Task<List<GameEvent>> StartTurn(OperationContext context);
}

public class TurnBasedGame(IExclusiveOperations exclusiveOperations, IPrimitives primitives)
    : BaseGame(exclusiveOperations, primitives), ITurnBasedGame
{
    private const string TurnTokenId = "turn";

    public async Task<List<GameEvent>> StartTurn(OperationContext context)
    {
        if (!await IsGameStarted(context))
            throw new Exception("Game not started");

        var controlTurnResult = await ExclusiveOperations.ControlObject(context, TurnTokenId);

        if (!controlTurnResult.Succeeded)
            throw new Exception("Turn already started");

        var activePlayerResult = await ExclusiveOperations.SetActivePlayerAsync(context);

        if (!activePlayerResult.Succeeded)
        {
            var revokeResult = await ExclusiveOperations.RevokeObjectControl(context, TurnTokenId);

            if (!revokeResult.Succeeded)
                throw new Exception("Could not revoke turn token");

            throw new Exception("Could not set active player");
        }

        return [..controlTurnResult.Events, ..activePlayerResult.Events];
    }
}
