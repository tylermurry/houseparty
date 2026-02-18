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
    public async Task CreateGame_ReturnsSuccess_AndBroadcastsAllEvents()
    {
        var gameEvents = new List<GameEvent>
        {
            new ClaimRoleEvent(PlayerId) { Sequence = 1, PlayerId = PlayerId, Timestamp = 100 },
            new GameCreatedEvent(GameId) { PlayerId = PlayerId, Timestamp = 100 }
        };

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.CreateGame(PlayerId, 4, It.IsAny<long>()))
            .ReturnsAsync(new CreateGameResult(GameId, gameEvents));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.CreateGame(new BaseGameExchanges.CreateGameRequest(PlayerId, 4), CancellationToken.None);

        result.GameCreated.Should().BeTrue();
        result.GameId.Should().Be(GameId);
        signalR.BroadcastCalls.Should().HaveCount(2);
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task JoinGame_ReturnsSuccess_AndBroadcastsSeatClaimEvent()
    {
        var gameEvents = new List<GameEvent>
        {
            new ControlObjectEvent("seat:1") { Sequence = 3, PlayerId = PlayerId, Timestamp = 102 },
            new PlayerJoinedGameEvent { PlayerId = PlayerId, Timestamp = 102 }
        };

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.JoinGame(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ReturnsAsync(gameEvents);

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.JoinGame(new BaseGameExchanges.JoinGameRequest(GameId, PlayerId), CancellationToken.None);

        result.Joined.Should().BeTrue();
        signalR.BroadcastCalls.Should().HaveCount(2);
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task StartGame_ReturnsSuccess_AndBroadcastsAllEvents()
    {
        var gameEvents = new List<GameEvent>
        {
            new GameStartedEvent { PlayerId = PlayerId, Timestamp = 103 }
        };

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.StartGame(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ReturnsAsync(gameEvents);

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.StartGame(new BaseGameExchanges.StartGameRequest(GameId, PlayerId), CancellationToken.None);

        result.GameStarted.Should().BeTrue();
        result.GameId.Should().Be(GameId);
        signalR.BroadcastCalls.Should().HaveCount(1);
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task EndGame_ReturnsSuccess_WhenGameIsEnded()
    {
        var gameEvents = new List<GameEvent>();

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.EndGame(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId)))
            .ReturnsAsync(gameEvents);

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.EndGame(new BaseGameExchanges.EndGameRequest(GameId, PlayerId), CancellationToken.None);

        result.GameEnded.Should().BeTrue();
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_ReturnsSuccess_AndBroadcastsAllEvents()
    {
        var gameEvents = new List<GameEvent>
        {
            new ReleaseObjectEvent(Policies.ActivePlayerTokenId) { Sequence = 5, PlayerId = PlayerId, Timestamp = 104 },
            new ReleaseObjectEvent(Policies.TurnTokenId) { Sequence = 6, PlayerId = PlayerId, Timestamp = 105 }
        };

        var turnBasedGame = new Mock<ITurnBasedGame>(MockBehavior.Strict);
        turnBasedGame
            .Setup(x => x.EndTurn(It.Is<OperationContext>(context => context.GameId == GameId && context.PlayerId == PlayerId), StatePayload))
            .ReturnsAsync((gameEvents, StatePayload));

        var signalR = new FakeRoomSignalRService();
        var controller = new TurnBasedGameController(turnBasedGame.Object, signalR);

        var result = await controller.EndTurn(new TurnBasedGameExchanges.EndTurnRequest(GameId, PlayerId, StatePayload), CancellationToken.None);

        result.TurnEnded.Should().BeTrue();
        signalR.BroadcastCalls.Should().HaveCount(2);
        signalR.StateSnapshotBroadcastCalls.Should().HaveCount(1);
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
        signalR.BroadcastCalls.Should().BeEmpty();
        turnBasedGame.VerifyAll();
    }
}
