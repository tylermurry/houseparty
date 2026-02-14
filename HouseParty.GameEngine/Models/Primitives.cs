namespace HouseParty.GameEngine.Models;

public sealed record TokenResult(bool Acquired, string? HolderId);

public sealed record CommitResult(bool Committed, long Revision);

public sealed record GameData(long Revision, string? Data);