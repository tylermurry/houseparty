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

    public async Task SendCounterToConnectionAsync(string connectionId, int count, CancellationToken cancellationToken)
    {
        var context = await GetHubContextAsync(cancellationToken);
        await context.Clients.Client(connectionId).SendAsync("counterUpdated", count, cancellationToken);
    }

    public async Task BroadcastCounterAsync(string roomId, int count, CancellationToken cancellationToken)
    {
        var context = await GetHubContextAsync(cancellationToken);
        await context.Clients.Group(RoomGroup(roomId)).SendAsync("counterUpdated", count, cancellationToken);
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
