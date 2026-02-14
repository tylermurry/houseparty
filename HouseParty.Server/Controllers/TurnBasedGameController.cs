using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Models.Exchange;
using HouseParty.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HouseParty.Server.Controllers;

[ApiController]
[Route("api/engine/turn-based-game")]
public sealed class TurnBasedGameController(ITurnBasedGame turnBasedGame, RoomSignalRService signalR) : ControllerBase
{
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

    // [HttpPost("submit-move")]
    // public async Task<SubmitMoveResponse> SubmitMove([FromBody] SubmitMoveRequest request, CancellationToken cancellationToken)
    // {
    //     // TODO: Get Active Player from ExclusiveOperations
    //     // TODO:
    //
    //
    //     var activePlayerCheck = await EnsureActivePlayerAsync(request.GameId, request.PlayerId, cancellationToken);
    //     if (activePlayerCheck is not null)
    //     {
    //         return activePlayerCheck;
    //     }
    //
    //     var context = request.ToContext();
    //     var gameEvent = await operations.Contested.SubmitActionAsync(context, request.EventName, request.Payload);
    //     await signalR.BroadcastGameEventAppendedAsync(request.GameId, gameEvent, cancellationToken);
    //     return Ok(gameEvent);
    // }
    //
    // [HttpPost("resolve")]
    // public async Task<ActionResult<CommitResponse>> Resolve([FromBody] ResolveRequest request, CancellationToken cancellationToken)
    // {
    //     var activePlayerCheck = await EnsureActivePlayerAsync(request.GameId, request.PlayerId, cancellationToken);
    //     if (activePlayerCheck is not null)
    //     {
    //         return activePlayerCheck;
    //     }
    //
    //     var context = request.ToContext();
    //     var commit = await operations.Commit.SetDataAsync(context, request.BaseRevision, request.Data);
    //     if (commit.Committed)
    //     {
    //         await operations.ReleaseTokenAsync(context, DefaultTurnTokenId);
    //         await operations.ReleaseActivePlayerAsync(context);
    //         var state = await BuildStateAsync(request.GameId, cancellationToken);
    //         await signalR.BroadcastGameStateCommittedAsync(request.GameId, state, cancellationToken);
    //     }
    //
    //     return Ok(new CommitResponse(commit.Committed, commit.Revision));
    // }
    //
    // [HttpPost("end-game")]
    // public async Task<ActionResult<GameEvent>> EndGame([FromBody] GameLifecycleRequest request, CancellationToken cancellationToken)
    // {
    //     var context = request.ToContext();
    //     var gameEvent = await operations.Lifecycle.EndGameAsync(context);
    //     await operations.RevokeActivePlayerAsync(context);
    //     await signalR.BroadcastGameEventAppendedAsync(request.GameId, gameEvent, cancellationToken);
    //     return Ok(gameEvent);
    // }
    //
    // [HttpPost("reset")]
    // public async Task<IActionResult> Reset([FromBody] GameLifecycleRequest request, CancellationToken cancellationToken)
    // {
    //     var context = request.ToContext();
    //     await operations.Lifecycle.ResetGameAsync(context);
    //     await operations.RevokeActivePlayerAsync(context);
    //     var state = await BuildStateAsync(request.GameId, cancellationToken);
    //     await signalR.BroadcastGameStateSnapshotAsync(request.GameId, state, cancellationToken);
    //     return Ok();
    // }
    //
    // [HttpGet("state")]
    // public async Task<ActionResult<GameStateResponse>> GetState([FromQuery] string gameId, CancellationToken cancellationToken)
    // {
    //     if (string.IsNullOrWhiteSpace(gameId))
    //     {
    //         return BadRequest("gameId is required.");
    //     }
    //
    //     var state = await BuildStateAsync(gameId, cancellationToken);
    //     return Ok(state);
    // }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}