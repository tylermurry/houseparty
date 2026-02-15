using FluentAssertions;
using HouseParty.GameEngine;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Models.Exchange;
using HouseParty.Server.Controllers.Engine;
using HouseParty.Server.Tests.Utils;
using Moq;

namespace HouseParty.Server.Tests.Controllers;

public sealed class TurnBasedGameControllerTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string StatePayload = "{\"phase\":\"main\",\"turn\":2}";

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

        var result = await controller.StartGame(new BaseGameExchanges.StartGameRequest(PlayerId), CancellationToken.None);

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

        var result = await controller.StartGame(new BaseGameExchanges.StartGameRequest(PlayerId), CancellationToken.None);

        result.GameStarted.Should().BeFalse();
        result.GameId.Should().BeNull();
        result.ErrorMessage.Should().Be("Game already started");
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

        var result = await controller.StopGame(new BaseGameExchanges.StopGameRequest(GameId, PlayerId), CancellationToken.None);

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

        var result = await controller.StopGame(new BaseGameExchanges.StopGameRequest(GameId, PlayerId), CancellationToken.None);

        result.GameStopped.Should().BeFalse();
        result.ErrorMessage.Should().Be("Could not stop game");
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_ReturnsFailure_WhenStartTurnThrows()
    {
        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.StartTurn(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ThrowsAsync(new Exception("Game not started"));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.StartTurn(new TurnBasedGameExchanges.StartTurnRequest(GameId, PlayerId), CancellationToken.None);

        result.TurnStarted.Should().BeFalse();
        result.ErrorMessage.Should().Be("Game not started");
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_ReturnsSuccess_AndBroadcastsAllEvents()
    {
        var gameEvents = new List<GameEvent>
        {
            new ReleaseObjectEvent(Policies.ActivePlayerTokenId) { Sequence = 3, PlayerId = PlayerId, Timestamp = 102 },
            new ReleaseObjectEvent(Policies.TurnTokenId) { Sequence = 4, PlayerId = PlayerId, Timestamp = 103 }
        };

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.EndTurn(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId), StatePayload))
            .ReturnsAsync((gameEvents, StatePayload));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.EndTurn(new TurnBasedGameExchanges.EndTurnRequest(GameId, PlayerId, StatePayload), CancellationToken.None);

        result.TurnEnded.Should().BeTrue();
        result.StatePayload.Should().Be(StatePayload);
        result.ErrorMessage.Should().BeNull();
        signalR.BroadcastCalls.Should().HaveCount(2);
        signalR.StateSnapshotBroadcastCalls.Should().HaveCount(1);
        signalR.BroadcastCalls[0].RoomId.Should().Be(GameId);
        signalR.BroadcastCalls[0].Payload.Should().Be(gameEvents[0]);
        signalR.BroadcastCalls[1].RoomId.Should().Be(GameId);
        signalR.BroadcastCalls[1].Payload.Should().Be(gameEvents[1]);
        signalR.StateSnapshotBroadcastCalls[0].RoomId.Should().Be(GameId);
        signalR.StateSnapshotBroadcastCalls[0].Payload.Should().Be(StatePayload);
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_ReturnsFailure_WhenEndTurnThrows()
    {
        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.EndTurn(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId), StatePayload))
            .ThrowsAsync(new Exception("No active turn"));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.EndTurn(new TurnBasedGameExchanges.EndTurnRequest(GameId, PlayerId, StatePayload), CancellationToken.None);

        result.TurnEnded.Should().BeFalse();
        result.StatePayload.Should().BeNull();
        result.ErrorMessage.Should().Be("No active turn");
        signalR.BroadcastCalls.Should().BeEmpty();
        signalR.StateSnapshotBroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task MakeMove_ReturnsSuccess_AndBroadcastsAllEvents()
    {
        const string move = "attack:slot-2";
        var gameEvents = new List<GameEvent>
        {
            new SubmitActionEvent(move) { Sequence = 3, PlayerId = PlayerId, Timestamp = 102 }
        };

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.MakeMove(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId), move))
            .ReturnsAsync(gameEvents);

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.MakeMove(new TurnBasedGameExchanges.MakeMoveRequest(GameId, PlayerId, move), CancellationToken.None);

        result.MoveAccepted.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        signalR.BroadcastCalls.Should().HaveCount(1);
        signalR.BroadcastCalls[0].RoomId.Should().Be(GameId);
        signalR.BroadcastCalls[0].Payload.Should().Be(gameEvents[0]);
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task MakeMove_ReturnsFailure_WhenMoveThrows()
    {
        const string move = "attack:slot-2";

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.MakeMove(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId), move))
            .ThrowsAsync(new Exception("Only active player can make a move"));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.MakeMove(new TurnBasedGameExchanges.MakeMoveRequest(GameId, PlayerId, move), CancellationToken.None);

        result.MoveAccepted.Should().BeFalse();
        result.ErrorMessage.Should().Be("Only active player can make a move");
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }
}
