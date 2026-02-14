using FluentAssertions;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests.Archetypes;

public sealed class TurnBasedGameTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string TurnTokenId = "turn";
    private const long Now = 123456789L;

    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    [Fact]
    public async Task StartTurn_Throws_WhenGameNotStarted()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync((string?)null);

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, primitives.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game not started");
        exclusiveOperations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_Throws_WhenTurnAlreadyStarted()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync("host-player");
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, TurnTokenId))
            .ReturnsAsync(new OperationResult(false, []));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, primitives.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Turn already started");
        exclusiveOperations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_Throws_WhenActivePlayerCannotBeSet_AndTurnTokenCannotBeRevoked()
    {
        var controlTurnEvents = new List<GameEvent> { new ControlObjectEvent(TurnTokenId) { Sequence = 1, PlayerId = PlayerId, Timestamp = Now } };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync("host-player");
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, TurnTokenId))
            .ReturnsAsync(new OperationResult(true, controlTurnEvents));
        exclusiveOperations
            .Setup(x => x.SetActivePlayerAsync(_context))
            .ReturnsAsync(new OperationResult(false, []));
        exclusiveOperations
            .Setup(x => x.RevokeObjectControl(_context, TurnTokenId))
            .ReturnsAsync(new OperationResult(false, []));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, primitives.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Could not revoke turn token");
        exclusiveOperations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_Throws_WhenActivePlayerCannotBeSet()
    {
        var controlTurnEvents = new List<GameEvent> { new ControlObjectEvent(TurnTokenId) { Sequence = 1, PlayerId = PlayerId, Timestamp = Now } };
        var revokeTurnEvents = new List<GameEvent> { new RevokeObjectEvent(TurnTokenId) { Sequence = 2, PlayerId = PlayerId, Timestamp = Now } };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync("host-player");
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, TurnTokenId))
            .ReturnsAsync(new OperationResult(true, controlTurnEvents));
        exclusiveOperations
            .Setup(x => x.SetActivePlayerAsync(_context))
            .ReturnsAsync(new OperationResult(false, []));
        exclusiveOperations
            .Setup(x => x.RevokeObjectControl(_context, TurnTokenId))
            .ReturnsAsync(new OperationResult(true, revokeTurnEvents));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, primitives.Object);

        var act = () => turnBasedGame.StartTurn(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Could not set active player");
        exclusiveOperations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StartTurn_Succeeds_WhenTurnCanStart()
    {
        var controlTurnEvents = new List<GameEvent> { new ControlObjectEvent(TurnTokenId) { Sequence = 1, PlayerId = PlayerId, Timestamp = Now } };
        var setActivePlayerEvents = new List<GameEvent> { new SetActivePlayerEvent(PlayerId) { Sequence = 2, PlayerId = PlayerId, Timestamp = Now } };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync("host-player");
        exclusiveOperations
            .Setup(x => x.ControlObject(_context, TurnTokenId))
            .ReturnsAsync(new OperationResult(true, controlTurnEvents));
        exclusiveOperations
            .Setup(x => x.SetActivePlayerAsync(_context))
            .ReturnsAsync(new OperationResult(true, setActivePlayerEvents));

        var turnBasedGame = new TurnBasedGame(exclusiveOperations.Object, primitives.Object);

        var events = await turnBasedGame.StartTurn(_context);

        events.Should().Equal(controlTurnEvents.Concat(setActivePlayerEvents));
        exclusiveOperations.VerifyAll();
        primitives.VerifyAll();
    }
}
