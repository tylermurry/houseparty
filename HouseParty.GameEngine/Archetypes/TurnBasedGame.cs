using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;

namespace HouseParty.GameEngine.Archetypes;

public interface ITurnBasedGame
{
    Task<List<GameEvent>> StartTurn(OperationContext context);
}

public class TurnBasedGame(IExclusiveOperations exclusiveOperations) : ITurnBasedGame
{
    private const string TurnTokenId = "turn";

    public async Task<List<GameEvent>> StartTurn(OperationContext context)
    {
        var controlTurnResult = await exclusiveOperations.ControlObject(context, TurnTokenId);

        if (!controlTurnResult.Succeeded)
            throw new Exception("Turn already started");

        var activePlayerResult = await exclusiveOperations.SetActivePlayerAsync(context);

        if (!activePlayerResult.Succeeded)
        {
            var revokeResult = await exclusiveOperations.RevokeObjectControl(context, TurnTokenId);

            if (!revokeResult.Succeeded)
                throw new Exception("Could not revoke turn token");

            throw new Exception("Could not set active player");
        }

        return [..controlTurnResult.Events, ..activePlayerResult.Events];
    }
}