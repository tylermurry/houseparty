using FluentAssertions;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests.Operations;

public sealed class GameOperationsTests
{
    private const string GameId = "game-1";
    private const string MetadataKey = "game:game-1:metadata";
    private const string PlayerId = "player-1";
    private const long Now = 123456789L;

    private readonly OperationContext _context = new(GameId, PlayerId, Now);
    private readonly Policies _policies = new(new Mock<IPrimitives>().Object);

    [Fact]
    public async Task ClearTokens_Fails_WhenCallerIsNotAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetValueAsync(MetadataKey))
            .ReturnsAsync("{\"status\":\"started\",\"totalSeats\":2,\"seatedPlayerIds\":[\"player-1\"],\"adminPlayerId\":\"player-2\"}");

        var operations = new GameOperations(_policies, primitives.Object);
        var result = await operations.ClearTokens(_context);

        result.Should().BeFalse();
        primitives.Verify(x => x.ClearTokensAsync(It.IsAny<string>()), Times.Never);
        primitives.VerifyAll();
    }

    [Fact]
    public async Task ClearTokens_Succeeds_WhenCallerIsAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetValueAsync(MetadataKey))
            .ReturnsAsync("{\"status\":\"started\",\"totalSeats\":2,\"seatedPlayerIds\":[\"player-1\"],\"adminPlayerId\":\"player-1\"}");
        primitives.Setup(x => x.ClearTokensAsync(GameId)).Returns(Task.CompletedTask);

        var operations = new GameOperations(_policies, primitives.Object);
        var result = await operations.ClearTokens(_context);

        result.Should().BeTrue();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task ClearGame_Succeeds_WhenCallerIsAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.SetupSequence(x => x.GetValueAsync(MetadataKey))
            .ReturnsAsync("{\"status\":\"started\",\"totalSeats\":2,\"seatedPlayerIds\":[\"player-1\"],\"adminPlayerId\":\"player-1\"}")
            .ReturnsAsync("{\"status\":\"started\",\"totalSeats\":2,\"seatedPlayerIds\":[\"player-1\"],\"adminPlayerId\":\"player-1\"}")
            .ReturnsAsync("{\"status\":\"started\",\"totalSeats\":2,\"seatedPlayerIds\":[\"player-1\"],\"adminPlayerId\":\"player-1\"}")
            .ReturnsAsync("{\"status\":\"started\",\"totalSeats\":2,\"seatedPlayerIds\":[\"player-1\"],\"adminPlayerId\":\"player-1\"}");
        primitives.Setup(x => x.ClearTokensAsync(GameId)).Returns(Task.CompletedTask);
        primitives.Setup(x => x.ClearEventsAsync(GameId)).Returns(Task.CompletedTask);
        primitives.Setup(x => x.ClearDataAsync(GameId)).Returns(Task.CompletedTask);
        primitives.Setup(x => x.DeleteValueAsync(MetadataKey)).Returns(Task.CompletedTask);

        var operations = new GameOperations(_policies, primitives.Object);
        var result = await operations.ClearGame(_context);

        result.Should().BeTrue();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task GetData_ReturnsDataFromPrimitives()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var expected = new GameData(3, "{\"phase\":\"play\"}");
        primitives.Setup(x => x.GetDataAsync(GameId)).ReturnsAsync(expected);

        var operations = new GameOperations(_policies, primitives.Object);
        var result = await operations.GetData(_context);

        result.Should().Be(expected);
        primitives.VerifyAll();
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsFromPrimitives()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        IReadOnlyList<GameEvent> expected =
        [
            new ControlObjectEvent("object-1")
            {
                Sequence = 1,
                PlayerId = "player-2",
                Timestamp = Now
            }
        ];
        primitives.Setup(x => x.GetEventsAsync(GameId)).ReturnsAsync(expected);

        var operations = new GameOperations(_policies, primitives.Object);
        var result = await operations.GetEvents(_context);

        result.Should().Equal(expected);
        primitives.VerifyAll();
    }

    [Fact]
    public async Task GetMetadata_ReturnsParsedMetadata_WhenPrimitiveHasMetadata()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetValueAsync(MetadataKey))
            .ReturnsAsync("{\"status\":\"created\",\"totalSeats\":3,\"seatedPlayerIds\":[\"player-1\"],\"adminPlayerId\":\"player-1\"}");

        var operations = new GameOperations(_policies, primitives.Object);
        var metadata = await operations.GetMetadata(_context);

        metadata.Should().NotBeNull();
        metadata!.Status.Should().Be("created");
        metadata.TotalSeats.Should().Be(3);
        metadata.SeatedPlayerIds.Should().ContainSingle().Which.Should().Be("player-1");
        metadata.AdminPlayerId.Should().Be("player-1");
        primitives.VerifyAll();
    }
}
