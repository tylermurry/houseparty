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
    private const string SeatToken = "seat:1";
    private const long Now = 123456789L;
    private readonly OperationContext _context = new(GameId, PlayerId, Now);

    [Fact]
    public async Task CreateGame_Throws_WhenSeatCountIsNotPositive()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);
        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var act = () => baseGame.CreateGame(PlayerId, 0, Now);

        await act.Should().ThrowAsync<Exception>().WithMessage("Seat count must be greater than zero");
    }

    [Fact]
    public async Task CreateGame_Succeeds_WhenMetadataIsInitialized()
    {
        var expectedClaimAdminEvents = new List<GameEvent>
        {
            new ClaimRoleEvent(PlayerId) { Sequence = 1, PlayerId = PlayerId, Timestamp = Now }
        };

        OperationContext? observedContext = null;

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        exclusiveOperations
            .Setup(x => x.ClaimRole(It.IsAny<OperationContext>(), Policies.AdminRoleId))
            .Callback<OperationContext, string>((context, _) => observedContext = context)
            .ReturnsAsync(new OperationResult(true, expectedClaimAdminEvents));
        gameOperations
            .Setup(x => x.GetMetadata(It.IsAny<OperationContext>()))
            .ReturnsAsync((GameMetadata?)null);
        gameOperations
            .Setup(x => x.SaveMetadata(It.IsAny<OperationContext>(), It.IsAny<GameMetadata>()))
            .Returns(Task.CompletedTask);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);

        var result = await baseGame.CreateGame(PlayerId, 4, Now);

        observedContext.Should().NotBeNull();
        Guid.TryParse(observedContext!.GameId, out _).Should().BeTrue();
        result.GameId.Should().Be(observedContext.GameId);
        result.Events.Should().HaveCount(2);
        result.Events[0].Should().BeEquivalentTo(expectedClaimAdminEvents[0]);
        result.Events[1].Should().BeOfType<GameCreatedEvent>();
        result.Events[1].As<GameCreatedEvent>().GameId.Should().Be(observedContext.GameId);
        result.Events[1].PlayerId.Should().Be(PlayerId);
        result.Events[1].Timestamp.Should().Be(Now);
        exclusiveOperations.VerifyAll();
        gameOperations.VerifyAll();
    }

    [Fact]
    public async Task JoinGame_Throws_WhenGameNotCreated()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync((GameMetadata?)null);
        policies.Setup(x => x.IsGameCreated(null)).Returns(false);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);
        var act = () => baseGame.JoinGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Game not created");
    }

    [Fact]
    public async Task JoinGame_Throws_WhenPlayerAlreadyJoined()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("created", 2, [PlayerId], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameCreated(metadata)).Returns(true);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(false);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(true);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);
        var act = () => baseGame.JoinGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Player already joined");
    }

    [Fact]
    public async Task JoinGame_Succeeds_WhenSeatIsAvailable()
    {
        var expectedEvents = new List<GameEvent>
        {
            new ControlObjectEvent(SeatToken) { Sequence = 3, PlayerId = PlayerId, Timestamp = Now }
        };

        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("created", 2, [], PlayerId);
        gameOperations
            .Setup(x => x.GetMetadata(_context))
            .ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameCreated(metadata)).Returns(true);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(false);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(false);
        policies.Setup(x => x.AreAllSeatsOccupied(metadata)).Returns(false);
        gameOperations
            .Setup(x => x.SaveMetadata(_context, It.IsAny<GameMetadata>()))
            .Returns(Task.CompletedTask);
        exclusiveOperations.Setup(x => x.ControlObject(_context, SeatToken)).ReturnsAsync(new OperationResult(true, expectedEvents));

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);
        var events = await baseGame.JoinGame(_context);

        events.Should().HaveCount(2);
        events[0].Should().BeEquivalentTo(expectedEvents[0]);
        events[1].Should().BeOfType<PlayerJoinedGameEvent>();
        events[1].PlayerId.Should().Be(PlayerId);
        events[1].Timestamp.Should().Be(Now);
        exclusiveOperations.VerifyAll();
        policies.VerifyAll();
    }

    [Fact]
    public async Task StartGame_Throws_WhenSeatsAreNotFull()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("created", 2, [PlayerId], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameCreated(metadata)).Returns(true);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(false);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(true);
        policies.Setup(x => x.AreAllSeatsOccupied(metadata)).Returns(false);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);
        var act = () => baseGame.StartGame(_context);

        await act.Should().ThrowAsync<Exception>().WithMessage("All seats must be occupied before starting");
    }

    [Fact]
    public async Task StartGame_Succeeds_WhenPlayerIsSeatedAndAllSeatsAreOccupied()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("created", 2, [PlayerId], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameCreated(metadata)).Returns(true);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(false);
        policies.Setup(x => x.IsPlayerSeated(metadata, PlayerId)).Returns(true);
        policies.Setup(x => x.AreAllSeatsOccupied(metadata)).Returns(true);
        gameOperations
            .Setup(x => x.SaveMetadata(_context, It.IsAny<GameMetadata>()))
            .Returns(Task.CompletedTask);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);
        var events = await baseGame.StartGame(_context);

        events.Should().ContainSingle();
        events[0].Should().BeOfType<GameStartedEvent>();
        events[0].PlayerId.Should().Be(PlayerId);
        events[0].Timestamp.Should().Be(Now);
        exclusiveOperations.VerifyNoOtherCalls();
        policies.VerifyAll();
    }

    [Fact]
    public async Task EndGame_Succeeds_WhenCalledByAdmin_AndClearGameSucceeds()
    {
        var exclusiveOperations = new Mock<IExclusiveOperations>(MockBehavior.Strict);
        var gameOperations = new Mock<IGameOperations>(MockBehavior.Strict);
        var policies = new Mock<IPolicies>(MockBehavior.Strict);

        var metadata = new GameMetadata("started", 2, [PlayerId], PlayerId);
        gameOperations.Setup(x => x.GetMetadata(_context)).ReturnsAsync(metadata);
        policies.Setup(x => x.IsGameStarted(metadata)).Returns(true);
        policies.Setup(x => x.IsPlayerAdminRole(metadata, PlayerId)).Returns(true);
        gameOperations.Setup(x => x.ClearGame(_context)).ReturnsAsync(true);

        var baseGame = new BaseGame(exclusiveOperations.Object, gameOperations.Object, policies.Object);
        var events = await baseGame.EndGame(_context);

        events.Should().BeEmpty();
        gameOperations.VerifyAll();
        policies.VerifyAll();
    }
}
