namespace HouseParty.GameEngine.Models.Exchange;

public static class TurnBasedGameExchanges
{
    public sealed record StartTurnRequest(string GameId, string PlayerId);
    public sealed record StartTurnResponse(bool TurnStarted, string? ErrorMessage = null);
    public sealed record MakeMoveRequest(string GameId, string PlayerId, string MovePayload);
    public sealed record MakeMoveResponse(bool MoveAccepted, string? ErrorMessage = null);
}
