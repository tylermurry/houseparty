using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;

namespace HouseParty.GameEngine.Operations;

public interface ICommitOperations
{
    Task<GameData> SaveData(OperationContext context, string payload);
}

public sealed class CommitOperations(IPrimitives primitives) : ICommitOperations
{
    public async Task<GameData> SaveData(OperationContext context, string payload)
    {
        var current = await primitives.GetDataAsync(context.GameId);
        var commit = await primitives.SetDataAsync(context.GameId, current.Revision, payload);

        if (commit.Committed)
            return new GameData(commit.Revision, payload);

        throw new Exception("Failed to save game data");
    }
}
