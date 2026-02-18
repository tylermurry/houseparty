namespace HouseParty.GameEngine.Models;

public sealed record GameMetadata(
    string Status,
    int TotalSeats,
    List<string> SeatedPlayerIds,
    string AdminPlayerId
);
