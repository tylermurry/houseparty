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
        // This call acquires the token for holder a
        var first = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-a");
        first.Acquired.Should().BeTrue();
        first.HolderId.Should().Be("holder-a");
        var holder = await _service.GetTokenHolderAsync(_gameId, _tokenId);
        holder.Should().Be("holder-a");

        // This call attempts to acquire a token for holder b but fails
        var second = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-b");
        second.Acquired.Should().BeFalse();
        second.HolderId.Should().Be("holder-a");
    }

    [Fact]
    public async Task Test_ReleaseToken()
    {
        // Acquire token for holder a
        var acquired = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-a");
        acquired.Acquired.Should().BeTrue();

        // Release holder a's token
        var released = await _service.ReleaseTokenAsync(_gameId, _tokenId);
        released.Should().BeTrue();
        var holderAfterRelease = await _service.GetTokenHolderAsync(_gameId, _tokenId);
        holderAfterRelease.Should().BeNull();

        // Ensure it can be re-acquired by holder b
        var reacquired = await _service.AcquireTokenAsync(_gameId, _tokenId, "holder-b");
        reacquired.Acquired.Should().BeTrue();
        reacquired.HolderId.Should().Be("holder-b");

        // Clear tokens and confirm the token can be acquired again
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
        var secondInput = new ReleaseActivePlayerEvent
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
        second.Name.Should().Be(nameof(ReleaseActivePlayerEvent));
        second.PlayerId.Should().Be("player-2");
        second.Timestamp.Should().Be(timestamp + 1);
        second.Should().BeEquivalentTo(secondInput, options => options.Excluding(e => e.Sequence));

        // Get the events and make sure they accurate and in the correct order
        var events = await _service.GetEventsAsync(_gameId);
        events.Should().Equal(first, second);

        // Clear events and confirm list is empty
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

        // Set initial data. The base revision should match allowing data to be set successfully
        var initial = await _service.SetDataAsync(_gameId, baseRevisionBeforeEdits, JsonSerializer.Serialize(data));
        initial.Committed.Should().BeTrue();
        initial.Revision.Should().Be(1);

        // Get data and verify it matches
        var stored = await _service.GetDataAsync(_gameId);
        stored.Revision.Should().Be(1);

        var gameData = JsonSerializer.Deserialize<MockData>(stored.Data!);
        gameData.Should().NotBeNull();
        gameData.Phase.Should().Be("setup");
        gameData.Score.Should().Be(5);

        // Attempt to set the data with the old base revision and confirm it is denied
        var mismatch = await _service.SetDataAsync(_gameId, baseRevisionBeforeEdits, JsonSerializer.Serialize(data));
        mismatch.Committed.Should().BeFalse();
        mismatch.Revision.Should().Be(1);

        // Clear data and confirm revision/data reset
        await _service.ClearDataAsync(_gameId);
        var cleared = await _service.GetDataAsync(_gameId);
        cleared.Revision.Should().Be(0);
        cleared.Data.Should().BeNull();
    }
}
