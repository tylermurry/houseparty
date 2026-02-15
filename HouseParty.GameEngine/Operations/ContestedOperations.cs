using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Operations;

public interface IContestedOperations
{
    Task<OperationResult> SubmitAction(OperationContext context, string actionPayload);
}

public sealed class ContestedOperations(IPrimitives primitives, IPolicies policies) : Operations(primitives), IContestedOperations
{
    private readonly IPrimitives _primitives = primitives;

    public async Task<OperationResult> SubmitAction(OperationContext context, string actionPayload)
    {
        await _primitives.AppendOrderedEventAsync(context.GameId, new SubmitActionEvent(actionPayload));

        return await SendGameEventAndBuildSuccessfulOperationResult(context, new SubmitActionEvent(actionPayload));
    }
}
