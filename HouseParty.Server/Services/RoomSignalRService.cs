using HouseParty.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;

namespace HouseParty.Server.Services;

public sealed class RoomSignalRService : IAsyncDisposable
{
    private const string HubName = "room";
    private readonly IServiceManager serviceManager;
    private readonly Lazy<Task<IServiceHubContext>> hubContext;

    public RoomSignalRService(IServiceManager serviceManager, ILoggerFactory loggerFactory)
    {
        this.serviceManager = serviceManager;
        hubContext = new Lazy<Task<IServiceHubContext>>(() => this.serviceManager.CreateHubContextAsync(HubName, loggerFactory));
    }

    public SignalRNegotiation Negotiate()
    {
        var endpoint = serviceManager.GetClientEndpoint(HubName);
        var accessToken = serviceManager.GenerateClientAccessToken(HubName);
        return new SignalRNegotiation(endpoint, accessToken);
    }

    public async Task AddToRoomAsync(string roomId, string connectionId, CancellationToken cancellationToken)
    {
        var context = await GetHubContextAsync(cancellationToken);
        await context.Groups.AddToGroupAsync(connectionId, RoomGroup(roomId), cancellationToken);
    }

    public async Task BroadcastPlayersAsync(string roomId, IReadOnlyList<RoomPlayer> players, CancellationToken cancellationToken)
    {
        var context = await GetHubContextAsync(cancellationToken);
        await context.Clients.Group(RoomGroup(roomId)).SendAsync("playerRosterUpdated", players, cancellationToken);
    }

    public async Task BroadcastMousePresenceAsync(string roomId, MousePresenceUpdate update, CancellationToken cancellationToken)
    {
        var context = await GetHubContextAsync(cancellationToken);
        await context.Clients.Group(RoomGroup(roomId)).SendAsync("mousePresenceUpdated", update, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!hubContext.IsValueCreated)
        {
            return;
        }

        var context = await hubContext.Value;
        await context.DisposeAsync();
    }

    private Task<IServiceHubContext> GetHubContextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return hubContext.Value;
    }

    private static string RoomGroup(string roomId) => $"room:{roomId}";
}
