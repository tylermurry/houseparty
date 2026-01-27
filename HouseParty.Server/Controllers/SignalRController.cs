using HouseParty.Server.Models;
using HouseParty.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace HouseParty.Server.Controllers;

[ApiController]
[Route("api/signalr")]
public sealed class SignalRController(RoomSignalRService signalR) : ControllerBase
{
    [HttpPost("negotiate")]
    public ActionResult<SignalRNegotiation> Negotiate()
    {
        var response = signalR.Negotiate();
        return Ok(response);
    }
}
