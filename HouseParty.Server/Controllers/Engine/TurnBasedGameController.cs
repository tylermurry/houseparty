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
    [HttpPost("create-game")]
    public async Task<BaseGameExchanges.CreateGameResponse> CreateGame([FromBody] BaseGameExchanges.CreateGameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var createGameResult = await turnBasedGame.CreateGame(request.PlayerId, request.SeatCount, Now());
            await BroadcastAllEvents(createGameResult.GameId, createGameResult.Events, cancellationToken);

            return new BaseGameExchanges.CreateGameResponse(true, createGameResult.GameId);
        }
        catch (Exception ex)
        {
            return new BaseGameExchanges.CreateGameResponse(false, null, ex.Message);
        }
    }

    [HttpPost("join-game")]
    public async Task<BaseGameExchanges.JoinGameResponse> JoinGame([FromBody] BaseGameExchanges.JoinGameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var gameEvents = await turnBasedGame.JoinGame(new OperationContext(request.GameId, request.PlayerId, Now()));
            await BroadcastAllEvents(request.GameId, gameEvents, cancellationToken);

            return new BaseGameExchanges.JoinGameResponse(true);
        }
        catch (Exception ex)
        {
            return new BaseGameExchanges.JoinGameResponse(false, ex.Message);
        }
    }

    [HttpPost("start-game")]
    public async Task<BaseGameExchanges.StartGameResponse> StartGame([FromBody] BaseGameExchanges.StartGameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var gameEvents = await turnBasedGame.StartGame(new OperationContext(request.GameId, request.PlayerId, Now()));
            await BroadcastAllEvents(request.GameId, gameEvents, cancellationToken);

            return new BaseGameExchanges.StartGameResponse(true, request.GameId);
        }
        catch (Exception ex)
        {
            return new BaseGameExchanges.StartGameResponse(false, null, ex.Message);
        }
    }

    [HttpPost("end-game")]
    public async Task<BaseGameExchanges.EndGameResponse> EndGame([FromBody] BaseGameExchanges.EndGameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var gameEvents = await turnBasedGame.EndGame(new OperationContext(request.GameId, request.PlayerId, Now()));
            await BroadcastAllEvents(request.GameId, gameEvents, cancellationToken);

            return new BaseGameExchanges.EndGameResponse(true);
        }
        catch (Exception ex)
        {
            return new BaseGameExchanges.EndGameResponse(false, ex.Message);
        }
    }

    [HttpPost("start-turn")]
    public async Task<TurnBasedGameExchanges.StartTurnResponse> StartTurn([FromBody] TurnBasedGameExchanges.StartTurnRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var gameEvents = await turnBasedGame.StartTurn(new OperationContext(request.GameId, request.PlayerId, Now()));
            await BroadcastAllEvents(request.GameId, gameEvents, cancellationToken);

            return new TurnBasedGameExchanges.StartTurnResponse(true);
        }
        catch (Exception ex)
        {
            return new TurnBasedGameExchanges.StartTurnResponse(false, ex.Message);
        }
    }

    [HttpPost("end-turn")]
    public async Task<TurnBasedGameExchanges.EndTurnResponse> EndTurn([FromBody] TurnBasedGameExchanges.EndTurnRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var endTurnResult = await turnBasedGame.EndTurn(new OperationContext(request.GameId, request.PlayerId, Now()), request.StatePayload);
            await BroadcastAllEvents(request.GameId, endTurnResult.Events, cancellationToken);
            await signalR.BroadcastGameStateSnapshot(request.GameId, endTurnResult.StatePayload, cancellationToken);

            return new TurnBasedGameExchanges.EndTurnResponse(true, endTurnResult.StatePayload);
        }
        catch (Exception ex)
        {
            return new TurnBasedGameExchanges.EndTurnResponse(false, null, ex.Message);
        }
    }

    [HttpPost("make-move")]
    public async Task<TurnBasedGameExchanges.MakeMoveResponse> MakeMove([FromBody] TurnBasedGameExchanges.MakeMoveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var gameEvents = await turnBasedGame.MakeMove(new OperationContext(request.GameId, request.PlayerId, Now()), request.MovePayload);
            await BroadcastAllEvents(request.GameId, gameEvents, cancellationToken);

            return new TurnBasedGameExchanges.MakeMoveResponse(true);
        }
        catch (Exception ex)
        {
            return new TurnBasedGameExchanges.MakeMoveResponse(false, ex.Message);
        }
    }

    private async Task BroadcastAllEvents(string roomId, List<GameEvent> events, CancellationToken cancellationToken)
    {
        foreach (var gameEvent in events)
            await signalR.BroadcastGameEvent(roomId, gameEvent, cancellationToken);
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
