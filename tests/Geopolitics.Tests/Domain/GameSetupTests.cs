using Geopolitics.Domain.Countries;
using Geopolitics.Domain.Games;
using Geopolitics.Domain.Players;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class GameSetupTests
{
    [Fact]
    public void Create_GivesEachOfTenPlayersOneCountryAtItsSoldierValue()
    {
        var game = TestGames.Create();

        Assert.Equal(10, game.Countries.Count);
        Assert.Equal(10, game.Players.Count);
        Assert.All(game.Players, player =>
        {
            var owned = Assert.Single(game.CountriesOwnedBy(player.Id));
            Assert.Equal(owned.Name, player.Name);
            Assert.Equal(owned.SoldierValue, owned.Soldiers);
        });
        Assert.Equal(9, game.GetCountry("IN").Soldiers);
        Assert.Equal(1, game.GetCountry("VA").Soldiers);
        Assert.Equal(5, game.GetCountry("ZA").Soldiers);
    }

    [Fact]
    public void Create_MakesOneHumanLinkedToTheOwnerAndNineNpcs()
    {
        var game = TestGames.Create();

        Assert.Equal(TestGames.OwnerUserId, game.OwnerUserId);
        Assert.Equal(TestGames.OwnerUserId, game.HumanPlayer.UserId);
        Assert.Equal("South Africa", game.HumanPlayer.Name);
        Assert.Equal(9, game.Players.Count(p => p.Kind == PlayerKind.Npc && p.UserId is null));
    }

    [Fact]
    public void Create_StartsRoundOneInActionPhaseWithNoReinforcements()
    {
        var game = TestGames.Create();

        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(1, game.Round);
        Assert.Equal(TurnPhase.Action, game.Phase);
        Assert.Equal(0, game.PendingReinforcements);
        Assert.Equal(game.TurnOrder[0], game.CurrentPlayer.Id);
    }

    [Fact]
    public void Create_TurnOrderContainsEveryPlayerOnce()
    {
        var game = TestGames.Create();

        Assert.Equal(game.Players.Select(p => p.Id).Order(), game.TurnOrder.Order());
    }

    [Fact]
    public void Create_ChoosesFirstPlayerAtRandom()
    {
        var firstPlayers = Enumerable.Range(0, 30)
            .Select(seed => TestGames.Create(seed))
            .Select(game => game.CurrentPlayer.Name)
            .Distinct()
            .Count();

        Assert.True(firstPlayers > 3, $"Only {firstPlayers} distinct first players across 30 games.");
    }

    [Fact]
    public void Create_RecordsGameStartedThenFirstTurnStarted()
    {
        var game = TestGames.Create();

        Assert.Collection(game.PendingEvents,
            e => Assert.Equal(game.TurnOrder, Assert.IsType<GameStarted>(e).TurnOrder),
            e => Assert.Equal(game.CurrentPlayer.Id, Assert.IsType<TurnStarted>(e).PlayerId));
    }

    [Fact]
    public void Create_RejectsWrongNumberOfOpponents()
    {
        Assert.Throws<ArgumentException>(() => Game.Create(
            Guid.NewGuid(), TestGames.OwnerUserId, TestGames.SouthAfrica, TestGames.Opponents.Take(8).ToList(), new Random()));
    }

    [Fact]
    public void Create_RejectsDuplicateCountries()
    {
        var opponents = TestGames.Opponents.Take(8).Append(new CountryProfile("za", "South Africa again", 1)).ToList();

        Assert.Throws<ArgumentException>(() => Game.Create(
            Guid.NewGuid(), TestGames.OwnerUserId, TestGames.SouthAfrica, opponents, new Random()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_RequiresAnOwner(string ownerUserId)
    {
        Assert.Throws<ArgumentException>(() => Game.Create(
            Guid.NewGuid(), ownerUserId, TestGames.SouthAfrica, TestGames.Opponents, new Random()));
    }

    [Fact]
    public void GetCountry_IsCaseInsensitiveAndRejectsUnknownCodes()
    {
        var game = TestGames.Create();

        Assert.Equal("India", game.GetCountry(" in ").Name);
        Assert.Throws<Geopolitics.Domain.GameRuleException>(() => game.GetCountry("FR"));
    }
}
