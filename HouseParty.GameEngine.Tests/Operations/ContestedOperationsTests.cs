using FluentAssertions;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests.Operations;

public sealed class ContestedOperationsTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string MovePayload = "attack:slot-2";
    private const long Now = 123456789L;

    [Fact]
    public async Task SubmitAction_AppendsActionTwice_AndReturnsEnrichedEvent()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        var context = new OperationContext(GameId, PlayerId, Now);

        var firstAppended = new SubmitActionEvent(MovePayload)
        {
            Sequence = 1,
            PlayerId = string.Empty,
            Timestamp = 0
        };

        var secondAppended = new SubmitActionEvent(MovePayload)
        {
            Sequence = 2,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        primitives
            .SetupSequence(x => x.AppendOrderedEventAsync(GameId, It.Is<GameEvent>(e =>
                e.GetType() == typeof(SubmitActionEvent) &&
                ((SubmitActionEvent)e).action == MovePayload)))
            .ReturnsAsync(firstAppended)
            .ReturnsAsync(secondAppended);

        var operations = new ContestedOperations(primitives.Object, policies.Object);

        var result = await operations.SubmitAction(context, MovePayload);

        result.Succeeded.Should().BeTrue();
        result.Events.Should().Equal(secondAppended);
        primitives.Verify(x => x.AppendOrderedEventAsync(GameId, It.IsAny<GameEvent>()), Times.Exactly(2));
        primitives.VerifyAll();
        policies.VerifyAll();
    }
}
