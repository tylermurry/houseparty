namespace HouseParty.GameEngine.Models.Exchange;

public static class TurnBasedGameExchanges
{
    public sealed record StartTurnRequest(string GameId, string PlayerId);
    public sealed record StartTurnResponse(bool TurnStarted, string? ErrorMessage = null);
}