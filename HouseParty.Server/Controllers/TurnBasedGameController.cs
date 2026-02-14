using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Models.Exchange;
using HouseParty.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HouseParty.Server.Controllers;

[ApiController]
[Route("api/engine/turn-based-game")]
public sealed class TurnBasedGameController(IBaseGame baseGame, ITurnBasedGame turnBasedGame, IRoomSignalRService signalR) : ControllerBase
{
    [HttpPost("start-game")]
    public async Task<BaseGameExchanges.StartGameResponse> StartGame([FromBody] BaseGameExchanges.StartGameRequest request)
    {
        try
        {
            var gameEvents = await baseGame.StartGame(new OperationContext(request.GameId, request.PlayerId, Now()));
            await BroadcastAllEvents(request.GameId, gameEvents);

            return new BaseGameExchanges.StartGameResponse(true);
        }
        catch (Exception ex)
        {
            return new BaseGameExchanges.StartGameResponse(false, ex.Message);
        }
    }

    [HttpPost("start-turn")]
    public async Task<TurnBasedGameExchanges.StartTurnResponse> StartTurn([FromBody] TurnBasedGameExchanges.StartTurnRequest request)
    {
        try
        {
            var gameEvents = await turnBasedGame.StartTurn(new OperationContext(request.GameId, request.PlayerId, Now()));
            await BroadcastAllEvents(request.GameId, gameEvents);

            return new TurnBasedGameExchanges.StartTurnResponse(true);
        }
        catch (Exception ex)
        {
            return new TurnBasedGameExchanges.StartTurnResponse(false, ex.Message);
        }
    }

    private async Task BroadcastAllEvents(string roomId, List<GameEvent> events)
    {
        foreach (var gameEvent in events)
            await signalR.BroadcastGameEvent(roomId, gameEvent, CancellationToken.None);
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
