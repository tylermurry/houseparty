using FluentAssertions;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests.Operations;

public sealed class CommitOperationsTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string StatePayload = "{\"phase\":\"main\",\"turn\":2}";
    private const long Now = 123456789L;

    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    [Fact]
    public async Task SaveData_Succeeds_WhenCommitSucceedsOnFirstAttempt()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives.Setup(x => x.GetDataAsync(GameId)).ReturnsAsync(new GameData(3, "{\"phase\":\"prep\"}"));
        primitives.Setup(x => x.SetDataAsync(GameId, 3, StatePayload)).ReturnsAsync(new CommitResult(true, 4));

        var operations = new CommitOperations(primitives.Object);

        var result = await operations.SaveData(_context, StatePayload);

        result.Revision.Should().Be(4);
        result.Data.Should().Be(StatePayload);
        primitives.VerifyAll();
    }

    [Fact]
    public async Task SaveData_Throws_WhenCommitFails()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);

        primitives.Setup(x => x.GetDataAsync(GameId)).ReturnsAsync(new GameData(1, "{\"phase\":\"prep\"}"));
        primitives.Setup(x => x.SetDataAsync(GameId, 1, StatePayload)).ReturnsAsync(new CommitResult(false, 2));

        var operations = new CommitOperations(primitives.Object);

        var act = () => operations.SaveData(_context, StatePayload);

        await act.Should().ThrowAsync<Exception>().WithMessage("Failed to save game data");
        primitives.VerifyAll();
    }
}
