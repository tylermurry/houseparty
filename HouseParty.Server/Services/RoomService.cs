using HouseParty.Server.Models;
using StackExchange.Redis;

namespace HouseParty.Server.Services;

public sealed class RoomService(IConnectionMultiplexer redis)
{
    private static readonly TimeSpan RoomTtl = TimeSpan.FromHours(24);

    public async Task CreateRoomAsync(string roomId)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(PlayerNumberKey(roomId), 0, RoomTtl);
    }

    private static string PlayerNumberKey(string roomId) => $"room:{roomId}:players:next";

    private static string PlayersKey(string roomId) => $"room:{roomId}:players";

    public async Task<(RoomPlayer Player, IReadOnlyList<RoomPlayer> Players)> JoinRoomAsync(string roomId, string name, int? playerNumber)
    {
        var db = redis.GetDatabase();
        var trimmedName = name.Trim();
        var assignedNumber = await ResolvePlayerNumberAsync(db, roomId, playerNumber);

        await db.HashSetAsync(PlayersKey(roomId), assignedNumber, trimmedName);
        await RefreshRoomTtlAsync(db, roomId);

        var players = await GetPlayersAsync(db, roomId);
        return (new RoomPlayer(assignedNumber, trimmedName), players);
    }

    private static async Task<int> ResolvePlayerNumberAsync(IDatabase db, string roomId, int? playerNumber)
    {
        if (playerNumber is > 0)
        {
            var existing = await db.HashGetAsync(PlayersKey(roomId), playerNumber.Value);
            if (existing.HasValue)
            {
                return playerNumber.Value;
            }
        }

        var newValue = await db.StringIncrementAsync(PlayerNumberKey(roomId));
        return (int)newValue;
    }

    private static async Task<IReadOnlyList<RoomPlayer>> GetPlayersAsync(IDatabase db, string roomId)
    {
        var entries = await db.HashGetAllAsync(PlayersKey(roomId));
        var players = new List<RoomPlayer>(entries.Length);

        foreach (var entry in entries)
        {
            if (!int.TryParse(entry.Name.ToString(), out var number))
            {
                continue;
            }

            players.Add(new RoomPlayer(number, entry.Value.ToString()));
        }

        players.Sort((left, right) => left.Number.CompareTo(right.Number));
        return players;
    }

    private static async Task RefreshRoomTtlAsync(IDatabase db, string roomId)
    {
        await db.KeyExpireAsync(PlayerNumberKey(roomId), RoomTtl);
        await db.KeyExpireAsync(PlayersKey(roomId), RoomTtl);
    }
}
