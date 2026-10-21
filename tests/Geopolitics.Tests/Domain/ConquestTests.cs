using Geopolitics.Domain;
using Geopolitics.Domain.Games;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class ConquestTests
{
    /// <summary>South Africa (5 soldiers) takes Vatican City (1) with a single winning die.</summary>
    private static (Game Game, Guid Human) HumanConquersVatican()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "VA");
        game.Roll(human, new ScriptedDiceRoller(6, 1));
        return (game, human);
    }

    private static void RollUntilAttackEnds(Game game, Guid player)
    {
        while (game.Phase == TurnPhase.AttackInProgress)
        {
            game.Roll(player, RiggedDiceRoller.AttackerWins());
        }
    }

    [Fact]
    public void Conquest_TransfersOwnershipWithTheMinimumBonus()
    {
        var (game, human) = HumanConquersVatican();
        var vatican = game.GetCountry("VA");

        // Brief p. 3: Vatican City (value 1) grants max(2, 1/2) = 2.
        Assert.Equal(human, vatican.OwnerId);
        Assert.Equal(2, vatican.Soldiers);
        Assert.Equal(5, game.GetCountry("ZA").Soldiers);
        Assert.Equal(TurnPhase.PostConquest, game.Phase);
        Assert.Equal(new AttackState("ZA", "VA"), game.ActiveAttack);
        var conquered = Assert.Single(game.PendingEvents.OfType<CountryConquered>());
        Assert.Equal(2, conquered.BonusSoldiers);
    }

    [Fact]
    public void Conquest_OfIndia_GrantsFourSoldiers()
    {
        // Brief p. 3: India (value 9) grants max(2, 9/2) = 4.
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");

        RollUntilAttackEnds(game, human);

        Assert.Equal(human, game.GetCountry("IN").OwnerId);
        Assert.Equal(4, game.GetCountry("IN").Soldiers);
    }

    [Fact]
    public void Conquest_OfAPlayersLastCountry_EliminatesThem()
    {
        var (game, human) = HumanConquersVatican();
        var vatican = game.Players.Single(p => p.Name == "Vatican City");

        Assert.True(vatican.IsEliminated);
        var eliminated = Assert.Single(game.PendingEvents.OfType<PlayerEliminated>());
        Assert.Equal((vatican.Id, human), (eliminated.PlayerId, eliminated.EliminatedById));
        Assert.Equal(GameStatus.InProgress, game.Status);
    }

    [Fact]
    public void MoveAfterConquest_MovesExtraSoldiersIn()
    {
        var (game, human) = HumanConquersVatican();

        game.MoveAfterConquest(human, 3);

        Assert.Equal((2, 5), (game.GetCountry("ZA").Soldiers, game.GetCountry("VA").Soldiers));
        Assert.Equal(TurnPhase.Action, game.Phase);
        Assert.Null(game.ActiveAttack);
        Assert.Equal(3, Assert.IsType<SoldiersMovedIn>(game.PendingEvents[^1]).Count);
    }

    [Fact]
    public void MoveAfterConquest_OfZero_KeepsSoldiersWhereTheyAre()
    {
        var (game, human) = HumanConquersVatican();

        game.MoveAfterConquest(human, 0);

        Assert.Equal((5, 2), (game.GetCountry("ZA").Soldiers, game.GetCountry("VA").Soldiers));
        Assert.Equal(TurnPhase.Action, game.Phase);
    }

    [Fact]
    public void MoveAfterConquest_MustLeaveOneSoldierBehind()
    {
        var (game, human) = HumanConquersVatican();

        var error = Assert.Throws<GameRuleException>(() => game.MoveAfterConquest(human, 5));

        Assert.Equal("South Africa must keep at least 1 soldier; you can move up to 4.", error.Message);
        Assert.Equal(TurnPhase.PostConquest, game.Phase);
        Assert.Throws<GameRuleException>(() => game.MoveAfterConquest(human, -1));
    }

    [Fact]
    public void MoveAfterConquest_WithoutAConquest_IsRejected()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");

        Assert.Throws<GameRuleException>(() => game.MoveAfterConquest(human, 1));
    }

    [Fact]
    public void BeforeTheConquestMove_OtherActionsAreRejected()
    {
        var (game, human) = HumanConquersVatican();

        Assert.Throws<GameRuleException>(() => game.Transfer(human, "ZA", "VA", 1));
        Assert.Throws<GameRuleException>(() => game.EndTurn(human));
        var error = Assert.Throws<GameRuleException>(() => game.StartAttack(human, "ZA", "IN"));
        Assert.Equal("Choose how many soldiers to move into the conquered country before attacking.", error.Message);
    }

    [Fact]
    public void Transfer_BetweenOwnCountries_AfterConquest()
    {
        var (game, human) = HumanConquersVatican();
        game.MoveAfterConquest(human, 0);

        game.Transfer(human, "ZA", "VA", 2);

        Assert.Equal((3, 4), (game.GetCountry("ZA").Soldiers, game.GetCountry("VA").Soldiers));
        var transferred = Assert.IsType<SoldiersTransferred>(game.PendingEvents[^1]);
        Assert.Equal(("ZA", "VA", 2), (transferred.FromCode, transferred.ToCode, transferred.Count));
    }

    [Fact]
    public void Transfer_MustLeaveOneSoldierBehind()
    {
        var (game, human) = HumanConquersVatican();
        game.MoveAfterConquest(human, 0);

        var error = Assert.Throws<GameRuleException>(() => game.Transfer(human, "VA", "ZA", 2));

        Assert.Equal("Vatican City must keep at least 1 soldier; you can move up to 1.", error.Message);
        Assert.Throws<GameRuleException>(() => game.Transfer(human, "VA", "ZA", 0));
        Assert.Equal((5, 2), (game.GetCountry("ZA").Soldiers, game.GetCountry("VA").Soldiers));
    }

    [Fact]
    public void Reinforcements_GrowWithCountriesControlled()
    {
        var (game, human) = HumanConquersVatican();
        game.MoveAfterConquest(human, 0);
        game.EndTurn(human);

        TestGames.PassUntil(game, "South Africa");
        Assert.Equal(4, game.PendingReinforcements);   // 2 countries

        TestGames.PlaceAllOn(game, "ZA");
        game.StartAttack(human, "ZA", "BW");
        RollUntilAttackEnds(game, human);
        game.MoveAfterConquest(human, 0);
        game.EndTurn(human);

        TestGames.PassUntil(game, "South Africa");
        Assert.Equal(6, game.PendingReinforcements);   // 3 countries, as in the brief
    }

    [Fact]
    public void LosingOneOfSeveralCountries_DoesNotEliminate()
    {
        var (game, human) = HumanConquersVatican();
        game.MoveAfterConquest(human, 0);
        game.EndTurn(human);
        var india = TestGames.PassUntil(game, "India");
        TestGames.PlaceAllOn(game, "IN");

        game.StartAttack(india, "IN", "VA");
        RollUntilAttackEnds(game, india);

        Assert.Equal(india, game.GetCountry("VA").OwnerId);
        Assert.False(game.HumanPlayer.IsEliminated);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.DoesNotContain(game.PendingEvents, e => e is PlayerEliminated eliminated && eliminated.PlayerId == human);
    }
}
