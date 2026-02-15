using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Operations;

public interface IGameOperations
{
    Task<bool> ClearTokens(OperationContext context);
    Task<bool> ClearEvents(OperationContext context);
    Task<bool> ClearData(OperationContext context);
    Task<bool> ClearGame(OperationContext context);
    Task<GameData> GetData(OperationContext context);
    Task<IReadOnlyList<GameEvent>> GetEvents(OperationContext context);
}

public class GameOperations(IPolicies policies, IPrimitives primitives) : Operations(primitives), IGameOperations
{
    private readonly IPrimitives _primitives = primitives;

    public async Task<bool> ClearTokens(OperationContext context)
    {
        if (!await policies.IsPlayerAdminRole(context.GameId, context.PlayerId))
            return false;

        await _primitives.ClearTokensAsync(context.GameId);
        return true;
    }

    public async Task<bool> ClearEvents(OperationContext context)
    {
        if (!await policies.IsPlayerAdminRole(context.GameId, context.PlayerId))
            return false;

        await _primitives.ClearEventsAsync(context.GameId);
        return true;
    }

    public async Task<bool> ClearData(OperationContext context)
    {
        if (!await policies.IsPlayerAdminRole(context.GameId, context.PlayerId))
            return false;

        await _primitives.ClearDataAsync(context.GameId);
        return true;
    }

    public async Task<bool> ClearGame(OperationContext context)
    {
        if (!await policies.IsPlayerAdminRole(context.GameId, context.PlayerId))
            return false;

        var tokensCleared = await ClearTokens(context);
        var eventsCleared = await ClearEvents(context);
        var dataCleared = await ClearData(context);

        return tokensCleared && eventsCleared && dataCleared;
    }

    public Task<GameData> GetData(OperationContext context) => _primitives.GetDataAsync(context.GameId);

    public Task<IReadOnlyList<GameEvent>> GetEvents(OperationContext context) => _primitives.GetEventsAsync(context.GameId);
}