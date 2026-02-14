namespace HouseParty.GameEngine.Models.Exchange;

public static class BaseGameExchanges
{
    public sealed record StartGameRequest(string GameId, string PlayerId);
    public sealed record StartGameResponse(bool GameStarted, string? ErrorMessage = null);
}
