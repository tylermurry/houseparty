using HouseParty.Server.Models;
using HouseParty.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HouseParty.Server.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsController(RoomService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RoomCreated>> CreateRoom()
    {
        var roomId = Guid.NewGuid().ToString("n");
        await service.CreateRoomAsync(roomId);

        return Created($"/room/{roomId}", new RoomCreated(roomId));
    }
}
