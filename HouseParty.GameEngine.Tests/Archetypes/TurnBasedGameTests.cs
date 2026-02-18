using FluentAssertions;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using Moq;

namespace HouseParty.GameEngine.Tests.Archetypes;

public sealed class TurnBasedGameTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string StatePayload = "{\"phase\":\"main\",\"turn\":2}";
    private const string MovePayload = "attack:slot-2";
    private const long Now = 123456789L;

    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    [Fact]
    public async Task StartTurn_Throws_WhenPlayerIsNotSeated()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("started", 2, [], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(true);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(false);

        var turnBasedGame = new TurnBasedGame(
            exclusiveOperations.Object,
            contestedOperations.Object,
            gameOperations.Object,
            commitOperations.Object,
            policies.Object);

        var act = () => turnBasedGame.StartTurn(_context);
        await act.Should().ThrowAsync<Exception>().WithMessage("Only seated players can perform this action");
    }

    [Fact]
    public async Task StartTurn_Succeeds_WhenTurnCanStart()
    {
        var controlTurnEvents = new List<GameEvent>
        {
            new ControlObjectEvent(Policies.TurnTokenId) { Sequence = 1, PlayerId = PlayerId, Timestamp = Now }
        };
        var setActivePlayerEvents = new List<GameEvent>
        {
            new ControlObjectEvent(Policies.ActivePlayerTokenId) { Sequence = 2, PlayerId = PlayerId, Timestamp = Now }
        };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("started", 2, [PlayerId], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(true);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(true);
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(true, controlTurnEvents));
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.ActivePlayerTokenId))
            .ReturnsAsync(new OperationResult(true, setActivePlayerEvents));

        var turnBasedGame = new TurnBasedGame(
            exclusiveOperations.Object,
            contestedOperations.Object,
            gameOperations.Object,
            commitOperations.Object,
            policies.Object);

        var events = await turnBasedGame.StartTurn(_context);

        events.Should().Equal(controlTurnEvents.Concat(setActivePlayerEvents));
        exclusiveOperations.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_Throws_WhenPlayerIsNotSeated()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("started", 2, [], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(true);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(false);

        var turnBasedGame = new TurnBasedGame(
            exclusiveOperations.Object,
            contestedOperations.Object,
            gameOperations.Object,
            commitOperations.Object,
            policies.Object);

        var act = () => turnBasedGame.EndTurn(_context, StatePayload);
        await act.Should().ThrowAsync<Exception>().WithMessage("Only seated players can perform this action");
    }

    [Fact]
    public async Task EndTurn_Succeeds_WhenActivePlayerEndsActiveTurn()
    {
        var releaseActivePlayerEvents = new List<GameEvent>
        {
            new ReleaseObjectEvent(Policies.ActivePlayerTokenId) { Sequence = 3, PlayerId = PlayerId, Timestamp = Now }
        };
        var releaseTurnEvents = new List<GameEvent>
        {
            new ReleaseObjectEvent(Policies.TurnTokenId) { Sequence = 4, PlayerId = PlayerId, Timestamp = Now }
        };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("started", 2, [PlayerId], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(true);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(true);
        policies.Setup(x => x.IsTurnActive(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(true);
        exclusiveOperations
            .Setup(x => x.ReleaseObjectControl(_context, Policies.ActivePlayerTokenId))
            .ReturnsAsync(new OperationResult(true, releaseActivePlayerEvents));
        exclusiveOperations
            .Setup(x => x.ReleaseObjectControl(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(true, releaseTurnEvents));
        commitOperations
            .Setup(x => x.SaveData(_context, StatePayload))
            .ReturnsAsync(new GameData(1, StatePayload));
        gameOperations
            .Setup(x => x.ClearEvents(_context))
            .ReturnsAsync(true);

        var turnBasedGame = new TurnBasedGame(
            exclusiveOperations.Object,
            contestedOperations.Object,
            gameOperations.Object,
            commitOperations.Object,
            policies.Object);

        var result = await turnBasedGame.EndTurn(_context, StatePayload);

        result.Events.Should().Equal(releaseActivePlayerEvents.Concat(releaseTurnEvents));
        result.StatePayload.Should().Be(StatePayload);
        exclusiveOperations.VerifyAll();
        commitOperations.VerifyAll();
        gameOperations.VerifyAll();
    }

    [Fact]
    public async Task MakeMove_Throws_WhenPlayerIsNotSeated()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("started", 2, [], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(true);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(false);

        var turnBasedGame = new TurnBasedGame(
            exclusiveOperations.Object,
            contestedOperations.Object,
            gameOperations.Object,
            commitOperations.Object,
            policies.Object);

        var act = () => turnBasedGame.MakeMove(_context, MovePayload);
        await act.Should().ThrowAsync<Exception>().WithMessage("Only seated players can perform this action");
    }

    [Fact]
    public async Task MakeMove_Succeeds_WhenPlayerIsActiveAndSeated()
    {
        var moveEvents = new List<GameEvent>
        {
            new SubmitActionEvent(MovePayload) { Sequence = 3, PlayerId = PlayerId, Timestamp = Now }
        };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("started", 2, [PlayerId], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(true);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(true);
        contestedOperations
            .Setup(x => x.SubmitAction(_context, MovePayload))
            .ReturnsAsync(new OperationResult(true, moveEvents));

        var turnBasedGame = new TurnBasedGame(
            exclusiveOperations.Object,
            contestedOperations.Object,
            gameOperations.Object,
            commitOperations.Object,
            policies.Object);

        var events = await turnBasedGame.MakeMove(_context, MovePayload);

        events.Should().Equal(moveEvents);
        contestedOperations.VerifyAll();
    }
}
