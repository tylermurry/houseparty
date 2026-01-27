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
        var payload = JsonSerializer.Serialize(new RoomStatePayload(roomId, 0));

        await db.StringSetAsync(RoomKey(roomId), payload, RoomTtl);
        await db.StringSetAsync(CounterKey(roomId), 0, RoomTtl);
    }

    private static string RoomKey(string roomId) => $"room:{roomId}";

    private static string CounterKey(string roomId) => $"room:{roomId}:counter";

    public async Task<int> GetCounterAsync(string roomId)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(CounterKey(roomId));
        return value.HasValue && int.TryParse(value.ToString(), out var count) ? count : 0;
    }

    public async Task<int> IncrementCounterAsync(string roomId)
    {
        var db = redis.GetDatabase();
        var newValue = await db.StringIncrementAsync(CounterKey(roomId));
        await db.KeyExpireAsync(CounterKey(roomId), RoomTtl);
        return (int)newValue;
    }
}
