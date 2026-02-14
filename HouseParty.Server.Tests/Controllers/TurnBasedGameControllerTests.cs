using FluentAssertions;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Models.Exchange;
using HouseParty.Server.Controllers;
using HouseParty.Server.Controllers.Engine;
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

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.StartGame(PlayerId, It.IsAny<long>()))
            .ReturnsAsync(new StartGameResult(GameId, gameEvents));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.StartGame(new BaseGameExchanges.StartGameRequest(PlayerId));

        result.GameStarted.Should().BeTrue();
        result.GameId.Should().Be(GameId);
        result.ErrorMessage.Should().BeNull();
        signalR.BroadcastCalls.Should().HaveCount(2);

        signalR.BroadcastCalls[0].RoomId.Should().Be(GameId);
        signalR.BroadcastCalls[0].Payload.Should().Be(gameEvents[0]);

        signalR.BroadcastCalls[1].RoomId.Should().Be(GameId);
        signalR.BroadcastCalls[1].Payload.Should().Be(gameEvents[1]);

        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task StartGame_ReturnsFailure_WhenGameStartThrows()
    {
        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.StartGame(PlayerId, It.IsAny<long>()))
            .ThrowsAsync(new Exception("Game already started"));


        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.StartGame(new BaseGameExchanges.StartGameRequest(PlayerId));

        result.GameStarted.Should().BeFalse();
        result.GameId.Should().BeNull();
        result.ErrorMessage.Should().Be("Game already started");
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_ReturnsFailure_WhenGameNotStarted()
    {
        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.StartTurn(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ThrowsAsync(new Exception("Game not started"));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.StartTurn(new TurnBasedGameExchanges.StartTurnRequest(GameId, PlayerId));

        result.TurnStarted.Should().BeFalse();
        result.ErrorMessage.Should().Be("Game not started");
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task StopGame_ReturnsSuccess_WhenGameIsStopped()
    {
        var gameEvents = new List<GameEvent>();

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.StopGame(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ReturnsAsync(gameEvents);

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.StopGame(new BaseGameExchanges.StopGameRequest(GameId, PlayerId));

        result.GameStopped.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        signalR.BroadcastCalls.Should().BeEmpty();

        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task StopGame_ReturnsFailure_WhenStopGameThrows()
    {
        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.StopGame(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ThrowsAsync(new Exception("Could not stop game"));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.StopGame(new BaseGameExchanges.StopGameRequest(GameId, PlayerId));

        result.GameStopped.Should().BeFalse();
        result.ErrorMessage.Should().Be("Could not stop game");
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }
}
