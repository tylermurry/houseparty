using FluentAssertions;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using Moq;

namespace HouseParty.GameEngine.Tests.Archetypes;

public sealed class BaseGameTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const long Now = 123456789L;
    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    [Fact]
    public async Task StartGame_Succeeds_WhenAdminRoleCanBeClaimed()
    {
        var expectedEvents = new List<GameEvent>
        {
            new ClaimRoleEvent(PlayerId)
            {
                Sequence = 1,
                PlayerId = PlayerId,
                Timestamp = Now
            }
        };

        OperationContext? claimRoleContext = null;

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        exclusiveOperations
            .Setup(x => x.ClaimRole(It.IsAny<OperationContext>(), Policies.AdminRoleId))
            .Callback<OperationContext, string>((context, _) => claimRoleContext = context)
            .ReturnsAsync(new OperationResult(true, expectedEvents));

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var result = await baseGame.StartGame(PlayerId, Now);

        claimRoleContext.Should().NotBeNull();
        claimRoleContext!.PlayerId.Should().Be(PlayerId);
        claimRoleContext.Now.Should().Be(Now);
        Guid.TryParse(claimRoleContext.GameId, out _).Should().BeTrue();

        result.GameId.Should().Be(claimRoleContext.GameId);
        result.Events.Should().Equal(expectedEvents);

        exclusiveOperations.VerifyAll();
        gameOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StartGame_Throws_WhenAdminRoleCannotBeClaimed()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        exclusiveOperations
            .Setup(x => x.ClaimRole(It.IsAny<OperationContext>(), Policies.AdminRoleId))
            .ReturnsAsync(new OperationResult(false, []));

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var act = () => baseGame.StartGame(PlayerId, Now);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game already started");
        exclusiveOperations.VerifyAll();
        gameOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StopGame_Throws_WhenGameNotStarted()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(false);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var act = () => baseGame.StopGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game not started");
        exclusiveOperations.VerifyAll();
        gameOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StopGame_Throws_WhenPlayerIsNotAdmin()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(false);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var act = () => baseGame.StopGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Only admin can stop game");
        exclusiveOperations.VerifyAll();
        gameOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StopGame_Throws_WhenClearGameFails()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(true);
        gameOperations.Setup(x => x.ClearGame(_context)).ReturnsAsync(false);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var act = () => baseGame.StopGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Failed to clear game data");
        exclusiveOperations.VerifyAll();
        gameOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StopGame_Succeeds_WhenCalledByAdmin_AndClearGameSucceeds()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        policies.Setup(x => x.IsGameStarted(GameId)).ReturnsAsync(true);
        policies.Setup(x => x.IsPlayerAdminRole(GameId, PlayerId)).ReturnsAsync(true);
        gameOperations.Setup(x => x.ClearGame(_context)).ReturnsAsync(true);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var events = await baseGame.StopGame(_context);

        events.Should().BeEmpty();
        exclusiveOperations.VerifyAll();
        gameOperations.VerifyAll();
        policies.VerifyAll();
    }
}
