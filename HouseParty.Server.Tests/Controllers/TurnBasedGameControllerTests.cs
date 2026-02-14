using FluentAssertions;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Models.Exchange;
using HouseParty.Server.Controllers;
using HouseParty.Server.Tests.Utils;
using Moq;

namespace HouseParty.Server.Tests.Controllers;

public sealed class TurnBasedGameControllerTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";

    [Fact]
    public async Task StartGame_ReturnsSuccess_AndBroadcastsAllEvents()
    {
        var gameEvents = new List<GameEvent>
        {
            new ClaimRoleEvent(PlayerId) { Sequence = 1, PlayerId = PlayerId, Timestamp = 100 },
            new ControlObjectEvent("table") { Sequence = 2, PlayerId = PlayerId, Timestamp = 101 }
        };

        var baseGame = new Mock<IBaseGame>(MockBehavior.Strict);
        baseGame
            .Setup(x => x.StartGame(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ReturnsAsync(gameEvents);

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(baseGame.Object, turnBasedGame.Object, signalR);

        var result = await controller.StartGame(new BaseGameExchanges.StartGameRequest(GameId, PlayerId));

        result.GameStarted.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        signalR.BroadcastCalls.Should().HaveCount(2);

        signalR.BroadcastCalls[0].RoomId.Should().Be(GameId);
        signalR.BroadcastCalls[0].Payload.Should().Be(gameEvents[0]);

        signalR.BroadcastCalls[1].RoomId.Should().Be(GameId);
        signalR.BroadcastCalls[1].Payload.Should().Be(gameEvents[1]);

        baseGame.VerifyAll();
    }

    [Fact]
    public async Task StartGame_ReturnsFailure_WhenGameStartThrows()
    {
        var baseGame = new Mock<IBaseGame>(MockBehavior.Strict);
        baseGame
            .Setup(x => x.StartGame(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ThrowsAsync(new Exception("Game already started"));

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(baseGame.Object, turnBasedGame.Object, signalR);

        var result = await controller.StartGame(new BaseGameExchanges.StartGameRequest(GameId, PlayerId));

        result.GameStarted.Should().BeFalse();
        result.ErrorMessage.Should().Be("Game already started");
        signalR.BroadcastCalls.Should().BeEmpty();
        baseGame.VerifyAll();
    }
}
