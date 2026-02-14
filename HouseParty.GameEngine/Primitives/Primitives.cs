using System.Text.Json;
using HouseParty.GameEngine.Models;
using StackExchange.Redis;

namespace HouseParty.GameEngine.Primitives;

public interface IPrimitives
{
    Task<TokenResult> AcquireTokenAsync(string gameId, string tokenId, string holderId, TimeSpan? ttl = null);
    Task<string?> GetTokenHolderAsync(string gameId, string tokenId);
    Task<bool> ReleaseTokenAsync(string gameId, string tokenId);
    Task ClearTokensAsync(string gameId);

    Task<GameEvent> AppendOrderedEventAsync(string gameId, GameEvent gameEvent);
    Task<IReadOnlyList<GameEvent>> GetEventsAsync(string gameId);
    Task ClearEventsAsync(string gameId);

    Task<CommitResult> SetDataAsync(string gameId, long baseRevision, string data);
    Task<GameData> GetDataAsync(string gameId);
    Task ClearDataAsync(string gameId);
}

public sealed class Primitives(IConnectionMultiplexer redis) : IPrimitives
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TokenResult> AcquireTokenAsync(string gameId, string tokenId, string holderId, TimeSpan? ttl = null)
    {
        var db = redis.GetDatabase();
        var key = PrimitiveKeys.TokenKey(gameId, tokenId);
        var acquired = await db.StringSetAsync(key, holderId, ttl ?? DefaultTtl, When.NotExists);

        if (acquired)
            await db.SetAddAsync(PrimitiveKeys.TokensKey(gameId), tokenId);

        RedisValue owner = acquired ? holderId : await db.StringGetAsync(key);
        return new TokenResult(acquired, owner.HasValue ? owner.ToString() : null);
    }

    public async Task<string?> GetTokenHolderAsync(string gameId, string tokenId)
    {
        var db = redis.GetDatabase();
        var holder = await db.StringGetAsync(PrimitiveKeys.TokenKey(gameId, tokenId));
        return holder.HasValue ? holder.ToString() : null;
    }

    public async Task<bool> ReleaseTokenAsync(string gameId, string tokenId)
    {
        var db = redis.GetDatabase();
        var deleted = await db.KeyDeleteAsync(PrimitiveKeys.TokenKey(gameId, tokenId));
        await db.SetRemoveAsync(PrimitiveKeys.TokensKey(gameId), tokenId);

        return deleted;
    }

    public async Task ClearTokensAsync(string gameId)
    {
        var db = redis.GetDatabase();
        var tokens = await db.SetMembersAsync(PrimitiveKeys.TokensKey(gameId));

        foreach (var token in tokens)
        {
            if (!token.HasValue)
                continue;

            await db.KeyDeleteAsync(PrimitiveKeys.TokenKey(gameId, token.ToString()));
        }

        await db.KeyDeleteAsync(PrimitiveKeys.TokensKey(gameId));
    }

    public async Task<GameEvent> AppendOrderedEventAsync(string gameId, GameEvent gameEvent)
    {
        var db = redis.GetDatabase();
        gameEvent.Sequence = await db.StringIncrementAsync(PrimitiveKeys.EventsSequenceKey(gameId));
        var json = JsonSerializer.Serialize(gameEvent, JsonOptions);

        await db.ListRightPushAsync(PrimitiveKeys.EventsKey(gameId), json);

        return gameEvent;
    }

    public async Task<IReadOnlyList<GameEvent>> GetEventsAsync(string gameId)
    {
        var db = redis.GetDatabase();
        var entries = await db.ListRangeAsync(PrimitiveKeys.EventsKey(gameId));

        if (entries.Length == 0)
            return [];

        var results = new List<GameEvent>(entries.Length);
        results.AddRange(
            from entry in entries
            where entry.HasValue
            select JsonSerializer.Deserialize<GameEvent>(entry.ToString(), JsonOptions)
        );

        return results;
    }

    public async Task ClearEventsAsync(string gameId)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(PrimitiveKeys.EventsKey(gameId));
        await db.KeyDeleteAsync(PrimitiveKeys.EventsSequenceKey(gameId));
    }

    public async Task<CommitResult> SetDataAsync(string gameId, long baseRevision, string data)
    {
        var db = redis.GetDatabase();
        var revisionKey = PrimitiveKeys.DataRevisionKey(gameId);
        var dataKey = PrimitiveKeys.DataKey(gameId);

        var transaction = db.CreateTransaction();

        transaction.AddCondition(baseRevision == 0
            ? Condition.KeyNotExists(revisionKey)
            : Condition.StringEqual(revisionKey, baseRevision));

        var nextRevision = baseRevision + 1;
        _ = transaction.StringSetAsync(dataKey, data);
        _ = transaction.StringSetAsync(revisionKey, nextRevision);

        if (await transaction.ExecuteAsync())
            return new CommitResult(true, nextRevision);

        var currentRevisionValue = await db.StringGetAsync(revisionKey);
        var currentRevision = currentRevisionValue.HasValue && long.TryParse(currentRevisionValue.ToString(), out var parsed)
            ? parsed
            : 0;

        return new CommitResult(false, currentRevision);
    }

    public async Task<GameData> GetDataAsync(string gameId)
    {
        var db = redis.GetDatabase();
        var keys = new RedisKey[] { PrimitiveKeys.DataRevisionKey(gameId), PrimitiveKeys.DataKey(gameId) };
        var values = await db.StringGetAsync(keys);

        var data = values.Length > 1 && values[1].HasValue ? values[1].ToString() : null;
        var revision = values.Length > 0 && values[0].HasValue && long.TryParse(values[0].ToString(), out var parsed)
            ? parsed
            : 0;

        return new GameData(revision, data);
    }

    public async Task ClearDataAsync(string gameId)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(PrimitiveKeys.DataKey(gameId));
        await db.KeyDeleteAsync(PrimitiveKeys.DataRevisionKey(gameId));
    }
}

internal static class PrimitiveKeys
{
    public static string TokenKey(string gameId, string tokenId) => $"game:{gameId}:token:{tokenId}";

    public static string TokensKey(string gameId) => $"game:{gameId}:tokens";

    public static string EventsKey(string gameId) => $"game:{gameId}:events";

    public static string EventsSequenceKey(string gameId) => $"game:{gameId}:events:seq";

    public static string DataKey(string gameId) => $"game:{gameId}:data";

    public static string DataRevisionKey(string gameId) => $"game:{gameId}:data:rev";
}
