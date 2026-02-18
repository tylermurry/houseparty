using FluentAssertions;
using HouseParty.GameEngine.Models;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests;

public sealed class PoliciesTests
{
    private const string PlayerId = "player-1";

    [Fact]
    public void IsGameCreated_ReturnsTrue_WhenMetadataExists()
    {
        var policies = new Policies(new Mock<IPrimitives>().Object);
        var metadata = new GameMetadata("created", 2, [], "player-1");

        var result = policies.IsGameCreated(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsGameStarted_ReturnsTrue_WhenMetadataStatusIsStarted()
    {
        var policies = new Policies(new Mock<IPrimitives>().Object);
        var metadata = new GameMetadata("started", 2, [PlayerId], "player-1");

        var result = policies.IsGameStarted(metadata);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsPlayerSeated_ReturnsTrue_WhenPlayerInMetadataSeatList()
    {
        var policies = new Policies(new Mock<IPrimitives>().Object);
        var metadata = new GameMetadata("created", 3, ["player-x", PlayerId], "player-1");

        var result = policies.IsPlayerSeated(metadata, PlayerId);

        result.Should().BeTrue();
    }

    [Fact]
    public void AreAllSeatsOccupied_ReturnsFalse_WhenSeatListSmallerThanCapacity()
    {
        var policies = new Policies(new Mock<IPrimitives>().Object);
        var metadata = new GameMetadata("created", 3, [PlayerId], "player-1");

        var result = policies.AreAllSeatsOccupied(metadata);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsPlayerAdminRole_ReturnsTrue_WhenAdminIdMatches()
    {
        var policies = new Policies(new Mock<IPrimitives>().Object);
        var metadata = new GameMetadata("created", 2, [], PlayerId);

        var result = policies.IsPlayerAdminRole(metadata, PlayerId);

        result.Should().BeTrue();
    }

}
