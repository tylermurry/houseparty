using FluentAssertions;
using HouseParty.GameEngine.Archetypes;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
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

        var operations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        operations
            .Setup(x => x.ClaimRole(It.IsAny<OperationContext>(), BaseGame.AdminRoleId))
            .Callback<OperationContext, string>((context, _) => claimRoleContext = context)
            .ReturnsAsync(new OperationResult(true, expectedEvents));

        var baseGame = new BaseGame(operations.Object, primitives.Object);

        var result = await baseGame.StartGame(PlayerId, Now);

        claimRoleContext.Should().NotBeNull();
        claimRoleContext!.PlayerId.Should().Be(PlayerId);
        claimRoleContext.Now.Should().Be(Now);
        Guid.TryParse(claimRoleContext.GameId, out _).Should().BeTrue();

        result.GameId.Should().Be(claimRoleContext.GameId);
        result.Events.Should().Equal(expectedEvents);

        operations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StartGame_Throws_WhenAdminRoleCannotBeClaimed()
    {
        var operations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        operations
            .Setup(x => x.ClaimRole(It.IsAny<OperationContext>(), BaseGame.AdminRoleId))
            .ReturnsAsync(new OperationResult(false, []));

        var baseGame = new BaseGame(operations.Object, primitives.Object);

        var act = () => baseGame.StartGame(PlayerId, Now);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game already started");
        operations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StopGame_Throws_WhenGameNotStarted()
    {
        var operations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync((string?)null);

        var baseGame = new BaseGame(operations.Object, primitives.Object);

        var act = () => baseGame.StopGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game not started");
        operations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StopGame_Throws_WhenPlayerIsNotAdmin()
    {
        var operations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync("host-player");

        var baseGame = new BaseGame(operations.Object, primitives.Object);

        var act = () => baseGame.StopGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Only admin can stop game");
        operations.VerifyAll();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task StopGame_Succeeds_WhenCalledByAdmin_AndClearsGameData()
    {
        var operations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(PlayerId);
        primitives
            .Setup(x => x.ClearGameAsync(GameId))
            .Returns(Task.CompletedTask);

        var baseGame = new BaseGame(operations.Object, primitives.Object);

        var events = await baseGame.StopGame(_context);

        events.Should().BeEmpty();
        operations.VerifyAll();
        primitives.VerifyAll();
    }
}
