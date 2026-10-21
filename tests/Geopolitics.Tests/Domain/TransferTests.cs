using Geopolitics.Domain;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

// Every player starts with one country, so successful transfers are covered in ConquestTests.
// These cover the rejections that apply from the first turn.
public class TransferTests
{
    [Fact]
    public void Transfer_ToACountryThePlayerDoesNotControl_IsRejected()
    {
        var game = TestGames.Create();
        var player = game.CurrentPlayer.Id;
        var own = game.CountriesOwnedBy(player)[0];
        var enemy = game.Countries.First(c => !c.IsOwnedBy(player));
        var (ownBefore, enemyBefore) = (own.Soldiers, enemy.Soldiers);

        var error = Assert.Throws<GameRuleException>(() => game.Transfer(player, own.Code, enemy.Code, 1));

        Assert.Equal("You can only transfer between countries you control.", error.Message);
        Assert.Equal((ownBefore, enemyBefore), (own.Soldiers, enemy.Soldiers));
    }

    [Fact]
    public void Transfer_FromACountryThePlayerDoesNotControl_IsRejected()
    {
        var game = TestGames.Create();
        var player = game.CurrentPlayer.Id;
        var own = game.CountriesOwnedBy(player)[0];
        var enemy = game.Countries.First(c => !c.IsOwnedBy(player));

        Assert.Throws<GameRuleException>(() => game.Transfer(player, enemy.Code, own.Code, 1));
    }

    [Fact]
    public void Transfer_ToTheSameCountry_IsRejected()
    {
        var game = TestGames.Create();
        var player = game.CurrentPlayer.Id;
        var own = game.CountriesOwnedBy(player)[0].Code;

        var error = Assert.Throws<GameRuleException>(() => game.Transfer(player, own, own, 1));

        Assert.Equal("Choose two different countries.", error.Message);
    }

    [Fact]
    public void Transfer_DuringReinforcePhase_IsRejected()
    {
        var game = TestGames.CreateInRoundTwo();
        var player = game.CurrentPlayer.Id;
        var own = game.CountriesOwnedBy(player)[0].Code;

        var error = Assert.Throws<GameRuleException>(() => game.Transfer(player, own, own, 1));

        Assert.Contains("reinforcements", error.Message);
    }

    [Fact]
    public void Transfer_ToAnUnknownCountry_IsRejected()
    {
        var game = TestGames.Create();
        var player = game.CurrentPlayer.Id;
        var own = game.CountriesOwnedBy(player)[0].Code;

        Assert.Throws<GameRuleException>(() => game.Transfer(player, own, "XX", 1));
    }
}
