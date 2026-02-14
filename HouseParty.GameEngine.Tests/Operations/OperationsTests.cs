using FluentAssertions;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests.Operations;

public sealed class OperationsTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string OtherPlayerId = "player-2";
    private const string AdminRoleId = "admin-role";
    private const long Now = 123456789L;

    private readonly Mock<IPrimitives> _primitives = new(MockBehavior.Strict);
    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    [Fact]
    public async Task ClearTokens_Fails_WhenCallerIsNotAdmin()
    {
        var service = new TestOperations(_primitives.Object);

        _primitives
            .Setup(p => p.GetTokenHolderAsync(GameId, AdminRoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await service.ClearTokensPublic(_context);

        result.Should().BeFalse();
        _primitives.Verify(p => p.ClearTokensAsync(It.IsAny<string>()), Times.Never);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClearTokens_Succeeds_WhenCallerIsAdmin()
    {
        var service = new TestOperations(_primitives.Object);

        _primitives
            .Setup(p => p.GetTokenHolderAsync(GameId, AdminRoleId))
            .ReturnsAsync(PlayerId);
        _primitives
            .Setup(p => p.ClearTokensAsync(GameId))
            .Returns(Task.CompletedTask);

        var result = await service.ClearTokensPublic(_context);

        result.Should().BeTrue();
        VerifyAndReset();
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsFromPrimitives()
    {
        var service = new TestOperations(_primitives.Object);
        IReadOnlyList<GameEvent> expected =
        [
            new ControlObjectEvent("object-1")
            {
                Sequence = 1,
                PlayerId = PlayerId,
                Timestamp = Now
            }
        ];

        _primitives
            .Setup(p => p.GetEventsAsync(GameId))
            .ReturnsAsync(expected);

        var result = await service.GetEventsPublic(_context);

        result.Should().Equal(expected);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClearEvents_Fails_WhenCallerIsNotAdmin()
    {
        var service = new TestOperations(_primitives.Object);

        _primitives
            .Setup(p => p.GetTokenHolderAsync(GameId, AdminRoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await service.ClearEventsPublic(_context);

        result.Should().BeFalse();
        _primitives.Verify(p => p.ClearEventsAsync(It.IsAny<string>()), Times.Never);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClearEvents_Succeeds_WhenCallerIsAdmin()
    {
        var service = new TestOperations(_primitives.Object);

        _primitives
            .Setup(p => p.GetTokenHolderAsync(GameId, AdminRoleId))
            .ReturnsAsync(PlayerId);
        _primitives
            .Setup(p => p.ClearEventsAsync(GameId))
            .Returns(Task.CompletedTask);

        var result = await service.ClearEventsPublic(_context);

        result.Should().BeTrue();
        VerifyAndReset();
    }

    [Fact]
    public async Task GetData_ReturnsDataFromPrimitives()
    {
        var service = new TestOperations(_primitives.Object);
        var expected = new GameData(3, "{\"phase\":\"play\"}");

        _primitives
            .Setup(p => p.GetDataAsync(GameId))
            .ReturnsAsync(expected);

        var result = await service.GetDataPublic(_context);

        result.Should().Be(expected);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClearData_Fails_WhenCallerIsNotAdmin()
    {
        var service = new TestOperations(_primitives.Object);

        _primitives
            .Setup(p => p.GetTokenHolderAsync(GameId, AdminRoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await service.ClearDataPublic(_context);

        result.Should().BeFalse();
        _primitives.Verify(p => p.ClearDataAsync(It.IsAny<string>()), Times.Never);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClearData_Succeeds_WhenCallerIsAdmin()
    {
        var service = new TestOperations(_primitives.Object);

        _primitives
            .Setup(p => p.GetTokenHolderAsync(GameId, AdminRoleId))
            .ReturnsAsync(PlayerId);
        _primitives
            .Setup(p => p.ClearDataAsync(GameId))
            .Returns(Task.CompletedTask);

        var result = await service.ClearDataPublic(_context);

        result.Should().BeTrue();
        VerifyAndReset();
    }

    private void VerifyAndReset()
    {
        _primitives.VerifyAll();
        _primitives.Reset();
    }

    private sealed class TestOperations(IPrimitives primitives) : GameEngine.Operations.Operations(primitives)
    {
        public Task<bool> ClearTokensPublic(OperationContext context) => ClearTokens(context);

        public Task<IReadOnlyList<GameEvent>> GetEventsPublic(OperationContext context) => GetEvents(context);

        public Task<bool> ClearEventsPublic(OperationContext context) => ClearEvents(context);

        public Task<GameData> GetDataPublic(OperationContext context) => GetData(context);

        public Task<bool> ClearDataPublic(OperationContext context) => ClearData(context);
    }
}
