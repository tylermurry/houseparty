using FluentAssertions;
using HouseParty.GameEngine.Archetypes;
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
    private const string ActivePlayerTokenId = "active-player";
    private const string RoleId = "judge-role";
    private const long Now = 123456789L;

    private readonly Mock<IPrimitives> _primitives = new(MockBehavior.Strict);
    private readonly Mock<IPrimitives> _tokens;
    private readonly Mock<IPrimitives> _events;
    private readonly IExclusiveOperations _operations;
    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    public ExclusiveOperationsTests()
    {
        _tokens = _primitives;
        _events = _primitives;
        _operations = new ExclusiveOperations(_primitives.Object);
    }

    [Fact]
    public async Task ControlObject_Succeeds_WhenTokenAcquired()
    {
        var expectedEvent = new ControlObjectEvent(ObjectId)
        {
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.AcquireTokenAsync(GameId, ObjectId, PlayerId, It.Is<TimeSpan?>(ttl => ttl == null)))
            .ReturnsAsync(new TokenResult(true, PlayerId));
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ControlObject(_context, ObjectId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task ControlObject_Fails_WhenTokenNotAcquired()
    {
        _tokens
            .Setup(p => p.AcquireTokenAsync(GameId, ObjectId, PlayerId, It.Is<TimeSpan?>(ttl => ttl == null)))
            .ReturnsAsync(new TokenResult(false, OtherPlayerId));

        var result = await _operations.ControlObject(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseObjectControl_Fails_WhenCallerDoesNotHoldToken()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, ObjectId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.ReleaseObjectControl(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseObjectControl_Fails_WhenTokenReleaseFails()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, ObjectId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ObjectId))
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
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, ObjectId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ObjectId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ReleaseObjectControl(_context, ObjectId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeObjectControl_Fails_WhenCallerIsNotAdmin()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.RevokeObjectControl(_context, ObjectId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeObjectControl_Fails_WhenReleaseFails()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ObjectId))
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
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ObjectId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.RevokeObjectControl(_context, ObjectId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task SetActivePlayerAsync_Fails_WhenTokenNotAcquired()
    {
        _tokens
            .Setup(p => p.AcquireTokenAsync(GameId, ActivePlayerTokenId, PlayerId, It.Is<TimeSpan?>(ttl => ttl == null)))
            .ReturnsAsync(new TokenResult(false, OtherPlayerId));

        var result = await _operations.SetActivePlayerAsync(_context);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task SetActivePlayerAsync_Succeeds_WhenTokenAcquired()
    {
        var expectedEvent = new SetActivePlayerEvent(PlayerId)
        {
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.AcquireTokenAsync(GameId, ActivePlayerTokenId, PlayerId, It.Is<TimeSpan?>(ttl => ttl == null)))
            .ReturnsAsync(new TokenResult(true, PlayerId));
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.SetActivePlayerAsync(_context);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseActivePlayerAsync_Fails_WhenCallerDoesNotHoldActivePlayerToken()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, ActivePlayerTokenId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.ReleaseActivePlayerAsync(_context);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseActivePlayerAsync_Fails_WhenReleaseFails()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, ActivePlayerTokenId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ActivePlayerTokenId))
            .ReturnsAsync(false);

        var result = await _operations.ReleaseActivePlayerAsync(_context);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseActivePlayerAsync_Succeeds_WhenCallerHoldsTokenAndReleaseSucceeds()
    {
        var expectedEvent = new ReleaseActivePlayerEvent
        {
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, ActivePlayerTokenId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ActivePlayerTokenId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ReleaseActivePlayerAsync(_context);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeActivePlayerAsync_Fails_WhenCallerIsNotAdmin()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.RevokeActivePlayerAsync(_context);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeActivePlayerAsync_Fails_WhenReleaseFails()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ActivePlayerTokenId))
            .ReturnsAsync(false);

        var result = await _operations.RevokeActivePlayerAsync(_context);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeActivePlayerAsync_Succeeds_WhenCallerIsAdminAndReleaseSucceeds()
    {
        var expectedEvent = new RevokeActivePlayerEvent
        {
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, ActivePlayerTokenId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.RevokeActivePlayerAsync(_context);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task ClaimRole_Fails_WhenTokenNotAcquired()
    {
        _tokens
            .Setup(p => p.AcquireTokenAsync(GameId, RoleId, PlayerId, It.Is<TimeSpan?>(ttl => ttl == null)))
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
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.AcquireTokenAsync(GameId, RoleId, PlayerId, It.Is<TimeSpan?>(ttl => ttl == null)))
            .ReturnsAsync(new TokenResult(true, PlayerId));
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ClaimRole(_context, RoleId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseRoleAsync_Fails_WhenCallerDoesNotHoldRole()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, RoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.ReleaseRoleAsync(_context, RoleId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task ReleaseRoleAsync_Fails_WhenReleaseFails()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, RoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, RoleId))
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
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, RoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, RoleId))
            .ReturnsAsync(true);
        SetupAppendOrderedEvent(expectedEvent);

        var result = await _operations.ReleaseRoleAsync(_context, RoleId);

        AssertSucceeded(result, expectedEvent);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeRoleAsync_Fails_WhenCallerIsNotAdmin()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(OtherPlayerId);

        var result = await _operations.RevokeRoleAsync(_context, RoleId);

        AssertFailed(result);
        VerifyAndReset();
    }

    [Fact]
    public async Task RevokeRoleAsync_Fails_WhenReleaseFails()
    {
        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, RoleId))
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
            Sequence = -1,
            PlayerId = PlayerId,
            Timestamp = Now
        };

        _tokens
            .Setup(p => p.GetTokenHolderAsync(GameId, BaseGame.AdminRoleId))
            .ReturnsAsync(PlayerId);
        _tokens
            .Setup(p => p.ReleaseTokenAsync(GameId, RoleId))
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
        _events
            .Setup(p => p.AppendOrderedEventAsync(GameId, It.Is<GameEvent>(gameEvent => gameEvent == expectedInput)))
            .ReturnsAsync(expectedEvent);
    }

    private void VerifyAndReset()
    {
        _primitives.VerifyAll();
        _primitives.Reset();
    }
}
