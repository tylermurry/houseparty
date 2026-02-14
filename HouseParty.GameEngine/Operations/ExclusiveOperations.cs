using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Operations;

public interface IExclusiveOperations
{
    Task<OperationResult> ControlObject(OperationContext context, string objectId);
    Task<OperationResult> ReleaseObjectControl(OperationContext context, string objectId);
    Task<OperationResult> RevokeObjectControl(OperationContext context, string objectId);
    Task<OperationResult> SetActivePlayerAsync(OperationContext context);
    Task<OperationResult> ReleaseActivePlayerAsync(OperationContext context);
    Task<OperationResult> RevokeActivePlayerAsync(OperationContext context);
    Task<OperationResult> ClaimRole(OperationContext context, string roleId);
    Task<OperationResult> ReleaseRoleAsync(OperationContext context, string roleId);
    Task<OperationResult> RevokeRoleAsync(OperationContext context, string roleId);
}

public sealed class ExclusiveOperations(IPrimitives primitives) : GameEngine.Operations.Operations(primitives), IExclusiveOperations
{
    private readonly IPrimitives _primitives = primitives;

    public async Task<OperationResult> ControlObject(OperationContext context, string objectId)
    {
        var result = await _primitives.AcquireTokenAsync(context.GameId, objectId, context.PlayerId);

        if (!result.Acquired)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new ControlObjectEvent(objectId));
    }

    public async Task<OperationResult> ReleaseObjectControl(OperationContext context, string objectId)
    {
        if (!await IsTokenHeldByPlayer(context, objectId))
            return new OperationResult(false, []);

        var released = await _primitives.ReleaseTokenAsync(context.GameId, objectId);

        if (!released)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new ReleaseObjectEvent(objectId));
    }

    public async Task<OperationResult> RevokeObjectControl(OperationContext context, string objectId)
    {
        if (!await IsPlayerAdminRole(context))
            return new OperationResult(false, []);

        var released = await _primitives.ReleaseTokenAsync(context.GameId, objectId);

        if (!released)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new RevokeObjectEvent(objectId));
    }

    public async Task<OperationResult> SetActivePlayerAsync(OperationContext context)
    {
        var result = await _primitives.AcquireTokenAsync(context.GameId, ActivePlayerTokenId, context.PlayerId);

        if (!result.Acquired)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new SetActivePlayerEvent(result.HolderId!));
    }

    public async Task<OperationResult> ReleaseActivePlayerAsync(OperationContext context)
    {
        if (!await IsTokenHeldByPlayer(context, ActivePlayerTokenId))
            return new OperationResult(false, []);

        var released = await _primitives.ReleaseTokenAsync(context.GameId, ActivePlayerTokenId);

        if (!released)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new ReleaseActivePlayerEvent());
    }

    public async Task<OperationResult> RevokeActivePlayerAsync(OperationContext context)
    {
        if (!await IsPlayerAdminRole(context))
            return new OperationResult(false, []);

        var released = await _primitives.ReleaseTokenAsync(context.GameId, ActivePlayerTokenId);

        if (!released)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new RevokeActivePlayerEvent());
    }

    public async Task<OperationResult> ClaimRole(OperationContext context, string roleId)
    {
        var result = await _primitives.AcquireTokenAsync(context.GameId, roleId, context.PlayerId);

        if (!result.Acquired)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new ClaimRoleEvent(result.HolderId!));
    }

    public async Task<OperationResult> ReleaseRoleAsync(OperationContext context, string roleId)
    {
        if (!await IsTokenHeldByPlayer(context, roleId))
            return new OperationResult(false, []);

        var released = await _primitives.ReleaseTokenAsync(context.GameId, roleId);

        if (!released)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new ReleaseRoleEvent());
    }

    public async Task<OperationResult> RevokeRoleAsync(OperationContext context, string roleId)
    {
        if (!await IsPlayerAdminRole(context))
            return new OperationResult(false, []);

        var released = await _primitives.ReleaseTokenAsync(context.GameId, roleId);

        if (!released)
            return new OperationResult(false, []);

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new RevokeRoleEvent());
    }
}
