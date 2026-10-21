using Geopolitics.Domain;
using Geopolitics.Domain.Games;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class AttackTests
{
    [Fact]
    public void StartAttack_EntersAttackPhaseAndRecordsAttackers()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        var india = game.GetCountry("IN");

        game.StartAttack(human, "ZA", "IN");

        Assert.Equal(TurnPhase.AttackInProgress, game.Phase);
        Assert.Equal(new AttackState("ZA", "IN"), game.ActiveAttack);
        Assert.True(game.HasAttackedThisTurn);
        var started = Assert.IsType<AttackStarted>(game.PendingEvents[^1]);
        Assert.Equal((human, india.OwnerId), (started.AttackerId, started.DefenderId));
    }

    [Fact]
    public void Roll_UsesBriefDiceRulesAndAppliesLosses()
    {
        // South Africa (5) attacks India (9): 4 dice each, since the attacker leaves one behind.
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");

        var round = game.Roll(human, new ScriptedDiceRoller(6, 1, 5, 2, /* vs */ 1, 1, 4, 3));

        Assert.Equal(4, round.AttackerDice.Count);
        Assert.Equal((1, 2), (round.AttackerLosses, round.DefenderLosses));   // 6>1, 1=1 tie, 5>4, 2<3
        Assert.Equal(4, game.GetCountry("ZA").Soldiers);
        Assert.Equal(7, game.GetCountry("IN").Soldiers);
        Assert.Equal(TurnPhase.AttackInProgress, game.Phase);
        var rolled = Assert.IsType<DiceRolled>(game.PendingEvents[^1]);
        Assert.Equal((4, 7), (rolled.AttackerSoldiersLeft, rolled.DefenderSoldiersLeft));
    }

    [Fact]
    public void Attack_EndsAsFailedWhenAttackerIsDownToOne_ButTurnContinues()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");

        game.Roll(human, RiggedDiceRoller.DefenderWins());   // 4 dice: attacker loses 4

        Assert.Equal(1, game.GetCountry("ZA").Soldiers);
        Assert.Equal(9, game.GetCountry("IN").Soldiers);
        Assert.Equal("India", game.GetPlayer(game.GetCountry("IN").OwnerId).Name);
        Assert.Equal(TurnPhase.Action, game.Phase);
        Assert.Null(game.ActiveAttack);
        Assert.Equal(human, game.CurrentPlayer.Id);
        Assert.IsType<AttackFailed>(game.PendingEvents[^1]);
    }

    [Fact]
    public void OnlyOneAttackPerTurn()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");
        game.Roll(human, RiggedDiceRoller.DefenderWins());

        var error = Assert.Throws<GameRuleException>(() => game.StartAttack(human, "ZA", "VA"));

        Assert.Equal("You have already attacked this turn.", error.Message);
    }

    [Fact]
    public void AttackAllowance_ResetsNextTurn()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");
        game.StopAttack(human);

        TestGames.PassUntil(game, "South Africa");

        Assert.False(game.HasAttackedThisTurn);
        TestGames.PlaceAllOn(game, "ZA");
        game.StartAttack(human, "ZA", "IN");
    }

    [Fact]
    public void StopAttack_KeepsRemainingSoldiersAndEndsTurn()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");
        game.Roll(human, new ScriptedDiceRoller(6, 1, 1, 1, /* vs */ 1, 6, 6, 6));   // ZA loses 3, IN loses 1

        game.StopAttack(human);

        Assert.Equal((2, 8), (game.GetCountry("ZA").Soldiers, game.GetCountry("IN").Soldiers));
        Assert.NotEqual(human, game.CurrentPlayer.Id);
        Assert.Null(game.ActiveAttack);
        Assert.Contains(game.PendingEvents, e => e is AttackStopped);
        Assert.Contains(game.PendingEvents, e => e is TurnEnded ended && ended.PlayerId == human);
    }

    [Fact]
    public void StartAttack_NeedsAtLeastTwoSoldiers()
    {
        var game = TestGames.Create();
        var vatican = TestGames.PassUntil(game, "Vatican City");

        var error = Assert.Throws<GameRuleException>(() => game.StartAttack(vatican, "VA", "ZA"));

        Assert.Equal("Vatican City needs at least 2 soldiers to attack.", error.Message);
        Assert.Equal(TurnPhase.Action, game.Phase);
    }

    [Fact]
    public void StartAttack_RejectsOwnTargetsAndForeignOrigins()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");

        Assert.Throws<GameRuleException>(() => game.StartAttack(human, "ZA", "ZA"));
        Assert.Throws<GameRuleException>(() => game.StartAttack(human, "IN", "VA"));
        Assert.False(game.HasAttackedThisTurn);
    }

    [Fact]
    public void StartAttack_IsRejectedBeforeReinforcementsArePlaced()
    {
        var game = TestGames.CreateInRoundTwo();
        var human = TestGames.PassUntil(game, "South Africa");

        var error = Assert.Throws<GameRuleException>(() => game.StartAttack(human, "ZA", "IN"));

        Assert.Equal("Place your reinforcements before attacking.", error.Message);
    }

    [Fact]
    public void DuringAnAttack_TransfersAndEndingTheTurnAreRejected()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");

        var transfer = Assert.Throws<GameRuleException>(() => game.Transfer(human, "ZA", "IN", 1));
        var endTurn = Assert.Throws<GameRuleException>(() => game.EndTurn(human));

        Assert.Equal("Stop or finish your attack before transferring soldiers.", transfer.Message);
        Assert.Equal("Stop or finish your attack before ending your turn.", endTurn.Message);
    }

    [Fact]
    public void RollAndStop_WithoutAnAttack_AreRejected()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");

        Assert.Throws<GameRuleException>(() => game.Roll(human, RiggedDiceRoller.AttackerWins()));
        Assert.Throws<GameRuleException>(() => game.StopAttack(human));
    }

    [Fact]
    public void Roll_ByAnotherPlayer_IsRejected()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "IN");
        var india = game.GetCountry("IN").OwnerId;

        Assert.Throws<GameRuleException>(() => game.Roll(india, RiggedDiceRoller.DefenderWins()));
        Assert.Equal(5, game.GetCountry("ZA").Soldiers);
    }
}
