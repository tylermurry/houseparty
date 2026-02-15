using FluentAssertions;
using HouseParty.GameEngine.Primitives;
using Moq;

namespace HouseParty.GameEngine.Tests;

public sealed class PoliciesTests
{
    private const string GameId = "game-1";
    private const string PlayerId = "player-1";

    [Fact]
    public async Task IsGameStarted_ReturnsFalse_WhenAdminRoleHasNoHolder()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.AdminRoleId)).ReturnsAsync((string?)null);

        var policies = new Policies(primitives.Object);

        var result = await policies.IsGameStarted(GameId);

        result.Should().BeFalse();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task IsGameStarted_ReturnsTrue_WhenAdminRoleHasHolder()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.AdminRoleId)).ReturnsAsync("host-player");

        var policies = new Policies(primitives.Object);

        var result = await policies.IsGameStarted(GameId);

        result.Should().BeTrue();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task IsPlayerAdminRole_ReturnsFalse_WhenHolderDoesNotMatch()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.AdminRoleId)).ReturnsAsync("host-player");

        var policies = new Policies(primitives.Object);

        var result = await policies.IsPlayerAdminRole(GameId, PlayerId);

        result.Should().BeFalse();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task IsPlayerAdminRole_ReturnsTrue_WhenHolderMatches()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.AdminRoleId)).ReturnsAsync(PlayerId);

        var policies = new Policies(primitives.Object);

        var result = await policies.IsPlayerAdminRole(GameId, PlayerId);

        result.Should().BeTrue();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task IsActivePlayer_ReturnsFalse_WhenHolderDoesNotMatch()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.ActivePlayerTokenId)).ReturnsAsync("player-2");

        var policies = new Policies(primitives.Object);

        var result = await policies.IsActivePlayer(GameId, PlayerId);

        result.Should().BeFalse();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task IsActivePlayer_ReturnsTrue_WhenHolderMatches()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.ActivePlayerTokenId)).ReturnsAsync(PlayerId);

        var policies = new Policies(primitives.Object);

        var result = await policies.IsActivePlayer(GameId, PlayerId);

        result.Should().BeTrue();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task IsTurnActive_ReturnsFalse_WhenTurnHasNoHolder()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.TurnTokenId)).ReturnsAsync((string?)null);

        var policies = new Policies(primitives.Object);

        var result = await policies.IsTurnActive(GameId);

        result.Should().BeFalse();
        primitives.VerifyAll();
    }

    [Fact]
    public async Task IsTurnActive_ReturnsTrue_WhenTurnHasHolder()
    {
        var primitives = new Mock<IPrimitives>(MockBehavior.Strict);
        primitives.Setup(x => x.GetTokenHolderAsync(GameId, Policies.TurnTokenId)).ReturnsAsync(PlayerId);

        var policies = new Policies(primitives.Object);

        var result = await policies.IsTurnActive(GameId);

        result.Should().BeTrue();
        primitives.VerifyAll();
    }
}
