using Geopolitics.Domain;
using Geopolitics.Domain.Games;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class ReinforcementTests
{
    // Brief p. 4: 1 country -> 2, 3 countries -> 6, 5 countries -> 10.
    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 6)]
    [InlineData(5, 10)]
    [InlineData(10, 20)]
    public void For_IsTwoPerCountryControlled(int countries, int expected)
    {
        Assert.Equal(expected, Reinforcements.For(countries));
    }

    [Fact]
    public void For_RejectsPlayersWithNoCountries()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Reinforcements.For(0));
    }

    [Fact]
    public void PlaceReinforcements_AddsSoldiersAndMovesToActionPhase()
    {
        var game = TestGames.CreateInRoundTwo();
        var player = game.CurrentPlayer.Id;
        var country = game.CountriesOwnedBy(player)[0];
        var before = country.Soldiers;

        game.PlaceReinforcements(player, new Dictionary<string, int> { [country.Code] = 2 });

        Assert.Equal(before + 2, country.Soldiers);
        Assert.Equal(0, game.PendingReinforcements);
        Assert.Equal(TurnPhase.Action, game.Phase);
        var placed = Assert.IsType<ReinforcementsPlaced>(game.PendingEvents[^1]);
        Assert.Equal(2, placed.Allocation[country.Code]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void PlaceReinforcements_MustPlaceExactlyTheAmountReceived(int count)
    {
        var game = TestGames.CreateInRoundTwo();
        var player = game.CurrentPlayer.Id;
        var code = game.CountriesOwnedBy(player)[0].Code;

        var error = Assert.Throws<GameRuleException>(() =>
            game.PlaceReinforcements(player, new Dictionary<string, int> { [code] = count }));

        Assert.Contains("exactly 2", error.Message);
        Assert.Equal(TurnPhase.Reinforce, game.Phase);
    }

    [Fact]
    public void PlaceReinforcements_RejectsCountriesThePlayerDoesNotControl()
    {
        var game = TestGames.CreateInRoundTwo();
        var player = game.CurrentPlayer.Id;
        var enemy = game.Countries.First(c => !c.IsOwnedBy(player));
        var enemySoldiers = enemy.Soldiers;

        Assert.Throws<GameRuleException>(() =>
            game.PlaceReinforcements(player, new Dictionary<string, int> { [enemy.Code] = 2 }));
        Assert.Equal(enemySoldiers, enemy.Soldiers);
    }

    [Fact]
    public void PlaceReinforcements_RejectsZeroOrNegativeCounts()
    {
        var game = TestGames.CreateInRoundTwo();
        var player = game.CurrentPlayer.Id;
        var own = game.CountriesOwnedBy(player)[0].Code;
        var enemy = game.Countries.First(c => !c.IsOwnedBy(player)).Code;

        Assert.Throws<GameRuleException>(() =>
            game.PlaceReinforcements(player, new Dictionary<string, int> { [own] = 3, [enemy] = -1 }));
        Assert.Throws<GameRuleException>(() =>
            game.PlaceReinforcements(player, new Dictionary<string, int>()));
    }

    [Fact]
    public void PlaceReinforcements_IsRejectedInRoundOne()
    {
        var game = TestGames.Create();
        var player = game.CurrentPlayer.Id;
        var code = game.CountriesOwnedBy(player)[0].Code;

        Assert.Throws<GameRuleException>(() =>
            game.PlaceReinforcements(player, new Dictionary<string, int> { [code] = 2 }));
    }

    [Fact]
    public void PlaceReinforcements_CannotBeRepeatedInTheSameTurn()
    {
        var game = TestGames.CreateInRoundTwo();
        var player = game.CurrentPlayer.Id;
        var code = game.CountriesOwnedBy(player)[0].Code;
        game.PlaceReinforcements(player, new Dictionary<string, int> { [code] = 2 });

        Assert.Throws<GameRuleException>(() =>
            game.PlaceReinforcements(player, new Dictionary<string, int> { [code] = 2 }));
    }
}
