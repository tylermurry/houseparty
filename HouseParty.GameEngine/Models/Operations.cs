namespace HouseParty.GameEngine.Models;

public sealed record OperationContext(string GameId, string PlayerId, long Now);
public sealed record OperationResult(bool Succeeded, List<GameEvent> Events);
