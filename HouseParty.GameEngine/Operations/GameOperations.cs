using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;
using System.Text.Json;

namespace HouseParty.GameEngine.Operations;

public interface IGameOperations
{
    Task<GameMetadata?> GetMetadata(OperationContext context);
    Task SaveMetadata(OperationContext context, GameMetadata metadata);
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static string MetadataKey(string gameId) => $"game:{gameId}:metadata";

    public async Task<GameMetadata?> GetMetadata(OperationContext context)
    {
        var metadataJson = await _primitives.GetValueAsync(MetadataKey(context.GameId));
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        return JsonSerializer.Deserialize<GameMetadata>(metadataJson, JsonOptions);
    }

    public async Task SaveMetadata(OperationContext context, GameMetadata metadata)
    {
        var metadataJson = JsonSerializer.Serialize(metadata, JsonOptions);
        await _primitives.SetValueAsync(MetadataKey(context.GameId), metadataJson);
    }

    public async Task<bool> ClearTokens(OperationContext context)
    {
        var metadata = await GetMetadata(context);

        if (!policies.IsPlayerAdminRole(metadata, context.PlayerId))
            return false;

        await _primitives.ClearTokensAsync(context.GameId);
        return true;
    }

    public async Task<bool> ClearEvents(OperationContext context)
    {
        var metadata = await GetMetadata(context);

        if (!policies.IsPlayerAdminRole(metadata, context.PlayerId))
            return false;

        await _primitives.ClearEventsAsync(context.GameId);
        return true;
    }

    public async Task<bool> ClearData(OperationContext context)
    {
        var metadata = await GetMetadata(context);

        if (!policies.IsPlayerAdminRole(metadata, context.PlayerId))
            return false;

        await _primitives.ClearDataAsync(context.GameId);
        return true;
    }

    public async Task<bool> ClearGame(OperationContext context)
    {
        var metadata = await GetMetadata(context);

        if (!policies.IsPlayerAdminRole(metadata, context.PlayerId))
            return false;

        var tokensCleared = await ClearTokens(context);
        var eventsCleared = await ClearEvents(context);
        var dataCleared = await ClearData(context);

        await _primitives.DeleteValueAsync(MetadataKey(context.GameId));

        return tokensCleared && eventsCleared && dataCleared;
    }

    public Task<GameData> GetData(OperationContext context) => _primitives.GetDataAsync(context.GameId);

    public Task<IReadOnlyList<GameEvent>> GetEvents(OperationContext context) => _primitives.GetEventsAsync(context.GameId);
}
