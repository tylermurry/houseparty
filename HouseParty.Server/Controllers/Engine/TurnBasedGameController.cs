using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Models.Exchange;
using HouseParty.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HouseParty.Server.Controllers.Engine;

[ApiController]
[Route("api/engine/turn-based-game")]
public sealed class TurnBasedGameController(ITurnBasedGame turnBasedGame, IRoomSignalRService signalR) : ControllerBase
{
    [HttpPost("start-game")]
    public async Task<BaseGameExchanges.StartGameResponse> StartGame([FromBody] BaseGameExchanges.StartGameRequest request)
    {
        try
        {
            var startGameResult = await turnBasedGame.StartGame(request.PlayerId, Now());
            await BroadcastAllEvents(startGameResult.GameId, startGameResult.Events);

            return new BaseGameExchanges.StartGameResponse(true, startGameResult.GameId);
        }
        catch (Exception ex)
        {
            return new BaseGameExchanges.StartGameResponse(false, null, ex.Message);
        }
    }

    [HttpPost("stop-game")]
    public async Task<BaseGameExchanges.StopGameResponse> StopGame([FromBody] BaseGameExchanges.StopGameRequest request)
    {
        try
        {
            var gameEvents = await turnBasedGame.StopGame(new OperationContext(request.GameId, request.PlayerId, Now()));
            await BroadcastAllEvents(request.GameId, gameEvents);

            return new BaseGameExchanges.StopGameResponse(true);
        }
        catch (Exception ex)
        {
            return new BaseGameExchanges.StopGameResponse(false, ex.Message);
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

    [HttpPost("make-move")]
    public async Task<TurnBasedGameExchanges.MakeMoveResponse> MakeMove([FromBody] TurnBasedGameExchanges.MakeMoveRequest request)
    {
        try
        {
            var gameEvents = await turnBasedGame.MakeMove(new OperationContext(request.GameId, request.PlayerId, Now()), request.MovePayload);
            await BroadcastAllEvents(request.GameId, gameEvents);

            return new TurnBasedGameExchanges.MakeMoveResponse(true);
        }
        catch (Exception ex)
        {
            return new TurnBasedGameExchanges.MakeMoveResponse(false, ex.Message);
        }
    }

    private async Task BroadcastAllEvents(string roomId, List<GameEvent> events)
    {
        foreach (var gameEvent in events)
            await signalR.BroadcastGameEvent(roomId, gameEvent, CancellationToken.None);
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
