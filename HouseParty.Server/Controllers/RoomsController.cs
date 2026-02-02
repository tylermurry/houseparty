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

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        await signalR.AddToRoomAsync(roomId, request.ConnectionId, cancellationToken);
        var joinResult = await service.JoinRoomAsync(roomId, request.Name, request.PlayerNumber);
        await signalR.BroadcastPlayersAsync(roomId, joinResult.Players, cancellationToken);

        return Ok(new RoomJoined(joinResult.Player, joinResult.Players));
    }

    [HttpPost("{roomId}/mouse")]
    public async Task<IActionResult> UpdateMousePresence(string roomId, [FromBody] MousePresenceUpdate request, CancellationToken cancellationToken)
    {
        if (request.PlayerNumber <= 0)
        {
            return BadRequest("PlayerNumber is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        if (request.X is < 0 or > 2047 || request.Y is < 0 or > 2047)
        {
            return BadRequest("Mouse coordinates are out of range.");
        }

        await signalR.BroadcastMousePresenceAsync(roomId, request, cancellationToken);
        return Accepted();
    }
}
