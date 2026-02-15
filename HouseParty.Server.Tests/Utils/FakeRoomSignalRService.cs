using HouseParty.Server.Models;
using HouseParty.Server.Services;

namespace HouseParty.Server.Tests.Utils;

public sealed class FakeRoomSignalRService : IRoomSignalRService
{
    public List<(string RoomId, object Payload)> BroadcastCalls { get; } = [];
    public List<(string RoomId, object Payload)> StateSnapshotBroadcastCalls { get; } = [];

    public SignalRNegotiation Negotiate() => throw new NotImplementedException();

    public Task AddToRoom(string roomId, string connectionId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task BroadcastPlayers(string roomId, IReadOnlyList<RoomPlayer> players, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task BroadcastMousePresence(string roomId, MousePresenceUpdate update, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task BroadcastGameEvent(string roomId, object payload, CancellationToken cancellationToken)
    {
        BroadcastCalls.Add((roomId, payload));
        return Task.CompletedTask;
    }

    public Task BroadcastGameStateSnapshot(string roomId, object payload, CancellationToken cancellationToken)
    {
        StateSnapshotBroadcastCalls.Add((roomId, payload));
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
