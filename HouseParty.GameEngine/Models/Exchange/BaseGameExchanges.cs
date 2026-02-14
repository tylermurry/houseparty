namespace HouseParty.GameEngine.Models.Exchange;

public static class BaseGameExchanges
{
    public sealed record StartGameRequest(string PlayerId);
    public sealed record StartGameResponse(bool GameStarted, string? GameId = null, string? ErrorMessage = null);
}
