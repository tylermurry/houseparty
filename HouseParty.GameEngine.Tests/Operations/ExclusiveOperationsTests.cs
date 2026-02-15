using FluentAssertions;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Operations;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests.Operations;

public sealed class ExclusiveOperationsTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";
    private const string OtherPlayerId = "player-2";
    private const string ObjectId = "object-1";
    private const string RoleId = "judge-role";
    private const long Now = 123456789L;

    private readonly Mock<IPrimitives> _primitives = new(MockBehavior.Strict);
    private readonly Mock<IPolicies> _policies = new(MockBehavior.Strict);
    private readonly IExclusiveOperations _operations;
    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    public ExclusiveOperationsTests()
    {
        _operations = new ExclusiveOperations(_primitives.Object, _policies.Object);
    }

    [Fact]
    public async Task ControlObject_Fails_WhenTokenNotAcquired()
    {
        _primitives
            .Setup(x => x.AcquireTokenAsync(GameId, ObjectId, PlayerId, null))
            .ReturnsAsync(new TokenResult(false, OtherPlayerId));

        var result = await _operations.ControlObject(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ControlObject_Succeeds_WhenTokenAcquired()
    {
        var expectedEvent = new ControlObjectEvent(ObjectId)
        {
            Sequence = 1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _primitives
            .Setup(x => x.AcquireTokenAsync(GameId, ObjectId, PlayerId, null))
            .ReturnsAsync(new TokenResult(true, PlayerId));
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ControlObject(_context, ObjectId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseObjectControl_Fails_WhenCallerDoesNotHoldToken()
    {
        _primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, ObjectId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.ReleaseObjectControl(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseObjectControl_Fails_WhenTokenReleaseFails()
    {
        _primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, ObjectId))
            .ReturnsAsync(PlayerId);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, ObjectId))
            .ReturnsAsync(false);

        var result = await _operations.ReleaseObjectControl(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseObjectControl_Succeeds_WhenCallerHoldsTokenAndReleaseSucceeds()
    {
        var expectedEvent = new ReleaseObjectEvent(ObjectId)
        {
            Sequence = 1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, ObjectId))
            .ReturnsAsync(PlayerId);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, ObjectId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ReleaseObjectControl(_context, ObjectId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeObjectControl_Fails_WhenCallerIsNotAdmin()
    {
        _policies
            .Setup(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(false);

        var result = await _operations.RevokeObjectControl(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeObjectControl_Fails_WhenReleaseFails()
    {
        _policies
            .Setup(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(true);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, ObjectId))
            .ReturnsAsync(false);

        var result = await _operations.RevokeObjectControl(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeObjectControl_Succeeds_WhenCallerIsAdminAndReleaseSucceeds()
    {
        var expectedEvent = new RevokeObjectEvent(ObjectId)
        {
            Sequence = 1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _policies
            .Setup(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(true);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, ObjectId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.RevokeObjectControl(_context, ObjectId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClaimRole_Fails_WhenTokenNotAcquired()
    {
        _primitives
            .Setup(x => x.AcquireTokenAsync(GameId, RoleId, PlayerId, null))
            .ReturnsAsync(new TokenResult(false, OtherPlayerId));

        var result = await _operations.ClaimRole(_context, RoleId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClaimRole_Succeeds_WhenTokenAcquired()
    {
        var expectedEvent = new ClaimRoleEvent(PlayerId)
        {
            Sequence = 1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _primitives
            .Setup(x => x.AcquireTokenAsync(GameId, RoleId, PlayerId, null))
            .ReturnsAsync(new TokenResult(true, PlayerId));
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ClaimRole(_context, RoleId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseRoleAsync_Fails_WhenCallerDoesNotHoldRole()
    {
        _primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, RoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.ReleaseRoleAsync(_context, RoleId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseRoleAsync_Fails_WhenReleaseFails()
    {
        _primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, RoleId))
            .ReturnsAsync(PlayerId);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, RoleId))
            .ReturnsAsync(false);

        var result = await _operations.ReleaseRoleAsync(_context, RoleId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseRoleAsync_Succeeds_WhenCallerHoldsRoleAndReleaseSucceeds()
    {
        var expectedEvent = new ReleaseRoleEvent
        {
            Sequence = 1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _primitives
            .Setup(x => x.GetTokenHolderAsync(GameId, RoleId))
            .ReturnsAsync(PlayerId);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, RoleId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ReleaseRoleAsync(_context, RoleId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeRoleAsync_Fails_WhenCallerIsNotAdmin()
    {
        _policies
            .Setup(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(false);

        var result = await _operations.RevokeRoleAsync(_context, RoleId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeRoleAsync_Fails_WhenReleaseFails()
    {
        _policies
            .Setup(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(true);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, RoleId))
            .ReturnsAsync(false);

        var result = await _operations.RevokeRoleAsync(_context, RoleId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeRoleAsync_Succeeds_WhenCallerIsAdminAndReleaseSucceeds()
    {
        var expectedEvent = new RevokeRoleEvent
        {
            Sequence = 1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _policies
            .Setup(x => x.IsPlayerAdminRole(GameId, PlayerId))
            .ReturnsAsync(true);
        _primitives
            .Setup(x => x.ReleaseTokenAsync(GameId, RoleId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.RevokeRoleAsync(_context, RoleId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    private static void AssertFailed(OperationResult result)
    {
        result.Succeeded.Should().BeFalse();
        result.Events.Should().BeEmpty();
    }

    private static void AssertSucceeded(OperationResult result, GameEvent expectedEvent)
    {
        result.Succeeded.Should().BeTrue();
        result.Events.Should().Equal(expectedEvent);
    }

    private void SetupAppendOrderedEvent<TEvent>(TEvent expectedEvent)
        where TEvent : GameEvent
    {
        var expectedInput = expectedEvent with { Sequence = 0 };
        _primitives
            .Setup(x => x.AppendOrderedEventAsync(GameId, It.Is<GameEvent>(gameEvent => gameEvent == expectedInput)))
            .ReturnsAsync(expectedEvent);
    }

    private void VerifyAndReset()
    {
        _primitives.VerifyAll();
        _primitives.Reset();
        _policies.VerifyAll();
        _policies.Reset();
    }
}
