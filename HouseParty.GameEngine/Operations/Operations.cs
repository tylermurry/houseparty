using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Operations;

public abstract class Operations(IPrimitives primitives)
{
    protected async Task<OperationResult> SendGameEventAndBuildSuccessfulOperationResult(OperationContext context, GameEvent gameEvent)
    {
        var enrichedEvent = gameEvent with
        {
            PlayerId = context.PlayerId,
            Timestamp = context.Now
        };

        var finalGameEvent = await primitives.AppendOrderedEventAsync(context.GameId, enrichedEvent);

        return new OperationResult(true, [finalGameEvent]);
    }
}
