using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Operations;

public abstract class Operations(IPrimitives primitives)
{
    protected const string ActivePlayerTokenId = "active-player";

    private const string AdminRoleId = "admin-role";

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

    protected async Task<bool> IsTokenHeldByPlayer(OperationContext context, string tokenId)
    {
        var holderId = await primitives.GetTokenHolderAsync(context.GameId, tokenId);
        return string.Equals(holderId, context.PlayerId, StringComparison.Ordinal);
    }

    protected Task<bool> IsPlayerAdminRole(OperationContext context) => IsTokenHeldByPlayer(context, AdminRoleId);

    protected async Task<bool> ClearTokens(OperationContext context)
    {
        if (!await IsPlayerAdminRole(context))
            return false;

        await primitives.ClearTokensAsync(context.GameId);
        return true;
    }

    protected Task<IReadOnlyList<GameEvent>> GetEvents(OperationContext context) =>
        primitives.GetEventsAsync(context.GameId);

    protected async Task<bool> ClearEvents(OperationContext context)
    {
        if (!await IsPlayerAdminRole(context))
            return false;

        await primitives.ClearEventsAsync(context.GameId);
        return true;
    }

    protected Task<GameData> GetData(OperationContext context) =>
        primitives.GetDataAsync(context.GameId);

    protected async Task<bool> ClearData(OperationContext context)
    {
        if (!await IsPlayerAdminRole(context))
            return false;

        await primitives.ClearDataAsync(context.GameId);
        return true;
    }
}
