using System.Text.Json;
using FluentAssertions;
using HouseParty.GameEngine.Models;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace HouseParty.GameEngine.Tests.Primitives;

[CollectionDefinition("Redis")]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7.2-alpine").Build();
    private IConnectionMultiplexer? _connection;

    public IConnectionMultiplexer Connection => _connection ?? throw new InvalidOperationException("Redis connection unavailable.");

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        var options = ConfigurationOptions.Parse(_redis.GetConnectionString());
        options.AbortOnConnectFail = false;
        _connection = await ConnectionMultiplexer.ConnectAsync(options);
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        await _redis.DisposeAsync();
    }
}

[Collection("Redis")]
public sealed class PrimitivesTests(RedisFixture redisFixture)
{
    private readonly GameEngine.Primitives.Primitives _service = new(redisFixture.Connection);
    private readonly string _gameId = $"game-{Guid.NewGuid():N}";
    private readonly string _tokenId = $"token-{Guid.NewGuid():N}";

    [Fact]
    public async Task Test_AcquireToken()
    {
        var first = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-a");
        first.Acquired.Should().BeTrue();
        first.HolderId.Should().Be("holder-a");
        var holder = await _service.GetTokenHolderAsync(_gameId, _tokenId);
        holder.Should().Be("holder-a");

        var second = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-b");
        second.Acquired.Should().BeFalse();
        second.HolderId.Should().Be("holder-a");
    }

    [Fact]
    public async Task Test_ReleaseToken()
    {
        var acquired = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-a");
        acquired.Acquired.Should().BeTrue();

        var released = await _service.ReleaseTokenAsync(_gameId, _tokenId);
        released.Should().BeTrue();
        var holderAfterRelease = await _service.GetTokenHolderAsync(_gameId, _tokenId);
        holderAfterRelease.Should().BeNull();

        var reacquired = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-b");
        reacquired.Acquired.Should().BeTrue();
        reacquired.HolderId.Should().Be("holder-b");

        await _service.ClearTokensAsync(_gameId);
        var clearedAcquire = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-c");
        clearedAcquire.Acquired.Should().BeTrue();
        clearedAcquire.HolderId.Should().Be("holder-c");
    }

    [Fact]
    public async Task Test_AppendOrderedEvent_And_GetEvents()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var firstInput = new ControlObjectEvent("object-1")
        {
            PlayerId = "player-1",
            Timestamp = timestamp
        };
        var secondInput = new ReleaseRoleEvent
        {
            PlayerId = "player-2",
            Timestamp = timestamp + 1
        };

        var first = await _service.AppendOrderedEventAsync(_gameId, firstInput);
        first.Sequence.Should().Be(1);
        first.Name.Should().Be(nameof(ControlObjectEvent));
        first.PlayerId.Should().Be("player-1");
        first.Timestamp.Should().Be(timestamp);
        first.Should().BeEquivalentTo(firstInput, options => options.Excluding(e => e.Sequence));

        var second = await _service.AppendOrderedEventAsync(_gameId, secondInput);
        second.Sequence.Should().Be(2);
        second.Name.Should().Be(nameof(ReleaseRoleEvent));
        second.PlayerId.Should().Be("player-2");
        second.Timestamp.Should().Be(timestamp + 1);
        second.Should().BeEquivalentTo(secondInput, options => options.Excluding(e => e.Sequence));

        var events = await _service.GetEventsAsync(_gameId);
        events.Should().Equal(first, second);

        await _service.ClearEventsAsync(_gameId);
        var cleared = await _service.GetEventsAsync(_gameId);
        cleared.Should().BeEmpty();
    }

    private record MockData(string Phase, int Score);

    [Fact]
    public async Task Test_SetAndGetData()
    {
        var data = new MockData("setup", 5);
        var baseRevisionBeforeEdits = 0;

        var initial = await _service.SetDataAsync(_gameId, baseRevisionBeforeEdits, JsonSerializer.Serialize(data));
        initial.Committed.Should().BeTrue();
        initial.Revision.Should().Be(1);

        var stored = await _service.GetDataAsync(_gameId);
        stored.Revision.Should().Be(1);

        var gameData = JsonSerializer.Deserialize<MockData>(stored.Data!);
        gameData.Should().NotBeNull();
        gameData!.Phase.Should().Be("setup");
        gameData.Score.Should().Be(5);

        var mismatch = await _service.SetDataAsync(_gameId, baseRevisionBeforeEdits, JsonSerializer.Serialize(data));
        mismatch.Committed.Should().BeFalse();
        mismatch.Revision.Should().Be(1);

        await _service.ClearDataAsync(_gameId);
        var cleared = await _service.GetDataAsync(_gameId);
        cleared.Revision.Should().Be(0);
        cleared.Data.Should().BeNull();
    }

    [Fact]
    public async Task Test_ClearMethods_RemoveAllKnownGameKeys()
    {
        await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-a");
        await _service.AppendOrderedEventAsync(_gameId, new ControlObjectEvent("object-1")
        {
            PlayerId = "player-1",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        await _service.SetDataAsync(_gameId, 0, JsonSerializer.Serialize(new MockData("running", 10)));

        await _service.ClearTokensAsync(_gameId);
        await _service.ClearEventsAsync(_gameId);
        await _service.ClearDataAsync(_gameId);

        var db = redisFixture.Connection.GetDatabase();
        var tokenHolder = await _service.GetTokenHolderAsync(_gameId, _tokenId);
        var events = await _service.GetEventsAsync(_gameId);
        var data = await _service.GetDataAsync(_gameId);

        tokenHolder.Should().BeNull();
        events.Should().BeEmpty();
        data.Revision.Should().Be(0);
        data.Data.Should().BeNull();

        (await db.KeyExistsAsync($"game:{_gameId}:tokens")).Should().BeFalse();
        (await db.KeyExistsAsync($"game:{_gameId}:events")).Should().BeFalse();
        (await db.KeyExistsAsync($"game:{_gameId}:events:seq")).Should().BeFalse();
        (await db.KeyExistsAsync($"game:{_gameId}:data")).Should().BeFalse();
        (await db.KeyExistsAsync($"game:{_gameId}:data:rev")).Should().BeFalse();
    }

    [Fact]
    public async Task Test_DefaultTtl_IsAppliedToAllWrittenKeys()
    {
        await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-a");
        await _service.AppendOrderedEventAsync(_gameId, new ControlObjectEvent("object-1")
        {
            PlayerId = "player-1",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        await _service.SetDataAsync(_gameId, 0, JsonSerializer.Serialize(new MockData("running", 10)));

        var db = redisFixture.Connection.GetDatabase();
        var keys = new[]
        {
            $"game:{_gameId}:token:{_tokenId}",
            $"game:{_gameId}:tokens",
            $"game:{_gameId}:events",
            $"game:{_gameId}:events:seq",
            $"game:{_gameId}:data",
            $"game:{_gameId}:data:rev"
        };

        foreach (var key in keys)
        {
            var ttl = await db.KeyTimeToLiveAsync(key);
            ttl.Should().NotBeNull();
            ttl!.Value.Should().BeGreaterThan(TimeSpan.FromHours(23));
            ttl.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(24));
        }
    }
}
