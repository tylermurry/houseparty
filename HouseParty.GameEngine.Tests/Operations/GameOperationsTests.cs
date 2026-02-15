using FluentAssertions;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests.Operations;

public sealed class GameOperationsTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string OtherPlayerId = "player-2";
    private const long Now = 123456789L;

    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    [Fact]
    public async Task ClearTokens_Fails_WhenCallerIsNotAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(false);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearTokens(_context);

        result.Should().BeFalse();
        primitives.Verify(x => x.ClearTokensAsync(It.IsAny<string>()), Times.Never);
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearTokens_Succeeds_WhenCallerIsAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(true);
        primitives.Setup(x => x.ClearTokensAsync(GameId)).Returns(Task.CompletedTask);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearTokens(_context);

        result.Should().BeTrue();
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearEvents_Fails_WhenCallerIsNotAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(false);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearEvents(_context);

        result.Should().BeFalse();
        primitives.Verify(x => x.ClearEventsAsync(It.IsAny<string>()), Times.Never);
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearEvents_Succeeds_WhenCallerIsAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(true);
        primitives.Setup(x => x.ClearEventsAsync(GameId)).Returns(Task.CompletedTask);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearEvents(_context);

        result.Should().BeTrue();
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearData_Fails_WhenCallerIsNotAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(false);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearData(_context);

        result.Should().BeFalse();
        primitives.Verify(x => x.ClearDataAsync(It.IsAny<string>()), Times.Never);
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearData_Succeeds_WhenCallerIsAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(true);
        primitives.Setup(x => x.ClearDataAsync(GameId)).Returns(Task.CompletedTask);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearData(_context);

        result.Should().BeTrue();
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearGame_Fails_WhenCallerIsNotAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(false);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearGame(_context);

        result.Should().BeFalse();
        primitives.Verify(x => x.ClearTokensAsync(It.IsAny<string>()), Times.Never);
        primitives.Verify(x => x.ClearEventsAsync(It.IsAny<string>()), Times.Never);
        primitives.Verify(x => x.ClearDataAsync(It.IsAny<string>()), Times.Never);
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearGame_Succeeds_WhenCallerIsAdmin()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.SetupSequence(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(true);

        primitives.Setup(x => x.ClearTokensAsync(GameId)).Returns(Task.CompletedTask);
        primitives.Setup(x => x.ClearEventsAsync(GameId)).Returns(Task.CompletedTask);
        primitives.Setup(x => x.ClearDataAsync(GameId)).Returns(Task.CompletedTask);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearGame(_context);

        result.Should().BeTrue();
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task ClearGame_Fails_WhenOneInnerClearIsDeniedByPolicy()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.SetupSequence(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(true)  // ClearGame gate
            .ReturnsAsync(true)  // ClearTokens
            .ReturnsAsync(false) // ClearEvents
            .ReturnsAsync(true); // ClearData

        primitives.Setup(x => x.ClearTokensAsync(GameId)).Returns(Task.CompletedTask);
        primitives.Setup(x => x.ClearDataAsync(GameId)).Returns(Task.CompletedTask);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.ClearGame(_context);

        result.Should().BeFalse();
        primitives.Verify(x => x.ClearTokensAsync(GameId), Times.Once);
        primitives.Verify(x => x.ClearEventsAsync(It.IsAny<string>()), Times.Never);
        primitives.Verify(x => x.ClearDataAsync(GameId), Times.Once);
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task GetData_ReturnsDataFromPrimitives()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        var expected = new GameData(3, "{\"phase\":\"play\"}");

        primitives
            .Setup(x => x.GetDataAsync(GameId))
            .ReturnsAsync(expected);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.GetData(_context);

        result.Should().Be(expected);
        primitives.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsFromPrimitives()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        IReadOnlyList<GameEvent> expected =
        [
            new ControlObjectEvent("object-1")
            {
                Sequence = 1,
                PlayerId = OtherPlayerId,
                Timestamp = Now
            }
        ];

        primitives
            .Setup(x => x.GetEventsAsync(GameId))
            .ReturnsAsync(expected);

        var operations = new GameOperations(policies.Object, primitives.Object);

        var result = await operations.GetEvents(_context);

        result.Should().Equal(expected);
        primitives.VerifyAll();
        policies.VerifyAll();
    }
}
