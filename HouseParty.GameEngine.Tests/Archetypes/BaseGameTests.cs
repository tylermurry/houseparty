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
    private const string AdminRoleId = "admin-role";
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

        var operations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        operations
            .Setup(x => x.ClaimRole(_context, AdminRoleId))
            .ReturnsAsync(new OperationResult(true, expectedEvents));

        var baseGame = new BaseGame(operations.Object);

        var result = await baseGame.StartGame(_context);

        result.Should().Equal(expectedEvents);
        operations.VerifyAll();
    }

    [Fact]
    public async Task StartGame_Throws_WhenAdminRoleCannotBeClaimed()
    {
        var operations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        operations
            .Setup(x => x.ClaimRole(_context, AdminRoleId))
            .ReturnsAsync(new OperationResult(false, []));

        var baseGame = new BaseGame(operations.Object);

        var act = () => baseGame.StartGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game already started");
        operations.VerifyAll();
    }
}
