namespace HouseParty.GameEngine.Models.Exchange;

public static class BaseGameExchanges
{
    public sealed record CreateGameRequest(string PlayerId, int SeatCount);
    public sealed record CreateGameResponse(bool GameCreated, string? GameId = null, string? ErrorMessage = null);
    public sealed record JoinGameRequest(string GameId, string PlayerId);
    public sealed record JoinGameResponse(bool Joined, string? ErrorMessage = null);
    public sealed record StartGameRequest(string GameId, string PlayerId);
    public sealed record StartGameResponse(bool GameStarted, string? GameId = null, string? ErrorMessage = null);
    public sealed record EndGameRequest(string GameId, string PlayerId);
    public sealed record EndGameResponse(bool GameEnded, string? ErrorMessage = null);
}
