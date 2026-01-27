using HouseParty.Server.Models;
using HouseParty.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HouseParty.Server.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController(RoomService service, RoomSignalRService signalR) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RoomCreated>> CreateRoom()
    {
        var roomId = Guid.NewGuid().ToString("n");
        await service.CreateRoomAsync(roomId);

        return Created($"/room/{roomId}", new RoomCreated(roomId));
    }

    [HttpPost("{roomId}/join")]
    public async Task<ActionResult<RoomJoined>> JoinRoom(string roomId, [FromBody] RoomJoinRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionId))
        {
            return BadRequest("ConnectionId is required.");
        }

        await signalR.AddToRoomAsync(roomId, request.ConnectionId, cancellationToken);
        var count = await service.GetCounterAsync(roomId);
        await signalR.SendCounterToConnectionAsync(request.ConnectionId, count, cancellationToken);

        return Ok(new RoomJoined(count));
    }

    [HttpPost("{roomId}/increment")]
    public async Task<ActionResult<RoomCounterUpdated>> IncrementRoom(string roomId, CancellationToken cancellationToken)
    {
        var count = await service.IncrementCounterAsync(roomId);
        await signalR.BroadcastCounterAsync(roomId, count, cancellationToken);

        return Ok(new RoomCounterUpdated(count));
    }
}
