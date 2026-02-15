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
    public async Task StartTurn_Throws_WhenGameNotStarted()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(false);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game not started");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_Throws_WhenTurnAlreadyStarted()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(false, []));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Turn already started");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_Throws_WhenActivePlayerCannotBeSet_AndTurnTokenCannotBeRevoked()
    {
        var controlTurnEvents = new List<GameEvent>
        {
            new ControlObjectEvent(Policies.TurnTokenId) { Sequence = 1, PlayerId = PlayerId, Timestamp = Now }
        };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(true, controlTurnEvents));
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.ActivePlayerTokenId))
            .ReturnsAsync(new OperationResult(false, []));
        exclusiveOperations
            .Setup(x => x.RevokeObjectControl(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(false, []));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Could not revoke turn token");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_Throws_WhenActivePlayerCannotBeSet()
    {
        var controlTurnEvents = new List<GameEvent>
        {
            new ControlObjectEvent(Policies.TurnTokenId) { Sequence = 1, PlayerId = PlayerId, Timestamp = Now }
        };
        var revokeTurnEvents = new List<GameEvent>
        {
            new RevokeObjectEvent(Policies.TurnTokenId) { Sequence = 2, PlayerId = PlayerId, Timestamp = Now }
        };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(true, controlTurnEvents));
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.ActivePlayerTokenId))
            .ReturnsAsync(new OperationResult(false, []));
        exclusiveOperations
            .Setup(x => x.RevokeObjectControl(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(true, revokeTurnEvents));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Could not set active player");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
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

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(true, controlTurnEvents));
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, Policies.ActivePlayerTokenId))
            .ReturnsAsync(new OperationResult(true, setActivePlayerEvents));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var events = await turnBasedGame.StartTurn(_context);

        events.Should().Equal(controlTurnEvents.Concat(setActivePlayerEvents));
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_Throws_WhenGameNotStarted()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(false);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.EndTurn(_context, StatePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game not started");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_Throws_WhenTurnIsNotActive()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsTurnActive(GameId)).ReturnsAsync(false);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.EndTurn(_context, StatePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("No active turn");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_Throws_WhenPlayerIsNotActive()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsTurnActive(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(false);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.EndTurn(_context, StatePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Only active player can end turn");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_Throws_WhenReleasingActivePlayerFails()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsTurnActive(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(true);
        exclusiveOperations
            .Setup(x => x.ReleaseObjectControl(_context, Policies.ActivePlayerTokenId))
            .ReturnsAsync(new OperationResult(false, []));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.EndTurn(_context, StatePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Failed to release active player");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_Throws_WhenReleasingTurnFails()
    {
        var releaseActivePlayerEvents = new List<GameEvent>
        {
            new ReleaseObjectEvent(Policies.ActivePlayerTokenId) { Sequence = 3, PlayerId = PlayerId, Timestamp = Now }
        };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsTurnActive(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(true);
        exclusiveOperations
            .Setup(x => x.ReleaseObjectControl(_context, Policies.ActivePlayerTokenId))
            .ReturnsAsync(new OperationResult(true, releaseActivePlayerEvents));
        exclusiveOperations
            .Setup(x => x.ReleaseObjectControl(_context, Policies.TurnTokenId))
            .ReturnsAsync(new OperationResult(false, []));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.EndTurn(_context, StatePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Failed to release turn");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
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

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
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

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var result = await turnBasedGame.EndTurn(_context, StatePayload);

        result.Events.Should().Equal(releaseActivePlayerEvents.Concat(releaseTurnEvents));
        result.StatePayload.Should().Be(StatePayload);
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task EndTurn_Throws_WhenClearEventsFails_AfterSavingState()
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

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
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
            .ReturnsAsync(false);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.EndTurn(_context, StatePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Failed to clear events");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task MakeMove_Throws_WhenGameNotStarted()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(false);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.MakeMove(_context, MovePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game not started");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task MakeMove_Throws_WhenPlayerIsNotActive()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(false);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.MakeMove(_context, MovePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Only active player can make a move");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task MakeMove_Throws_WhenMoveIsEmpty()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var contestedOperations = new Mock<IContestedOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var commitOperations = new Mock<ICommitOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(true);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var act = () => turnBasedGame.MakeMove(_context, " ");

        await act.Should().ThrowAsync<Exception>().WithMessage("Move is required");
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task MakeMove_Succeeds_WhenPlayerIsActive()
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

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsActivePlayer(GameId, PlayerId)).ReturnsAsync(true);
        contestedOperations
            .Setup(x => x.SubmitAction(_context, MovePayload))
            .ReturnsAsync(new OperationResult(true, moveEvents));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, contestedOperations.Object, gameOperations.Object, commitOperations.Object, policies.Object);

        var events = await turnBasedGame.MakeMove(_context, MovePayload);

        events.Should().Equal(moveEvents);
        exclusiveOperations.VerifyAll();
        contestedOperations.VerifyAll();
        gameOperations.VerifyAll();
        commitOperations.VerifyAll();
        policies.VerifyAll();
    }
}
