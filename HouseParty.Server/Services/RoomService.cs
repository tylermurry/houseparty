using System.Text.Json;
using HouseParty.Server.Models;
using StackExchange.Redis;

namespace HouseParty.Server.Services;

public sealed class RoomService(IConnectionMultiplexer redis)
{
    private static readonly TimeSpan RoomTtl = TimeSpan.FromHours(24);

    public async Task CreateRoomAsync(string roomId)
    {
        var db = redis.GetDatabase();
        var payload = JsonSerializer.Serialize(new RoomStatePayload(roomId));

        await db.StringSetAsync(RoomKey(roomId), payload, RoomTtl);
    }

    private static string RoomKey(string roomId) => $"room:{roomId}";
}
