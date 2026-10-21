using Geopolitics.Domain;
using Geopolitics.Domain.Games;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class GameEndTests
{
    [Fact]
    public void HumanLosingTheirLastCountry_EndsTheGameAsALoss()
    {
        var game = TestGames.Create();
        var india = TestGames.PassUntil(game, "India");
        TestGames.PlaceAllOn(game, "IN");

        game.StartAttack(india, "IN", "ZA");
        game.Roll(india, RiggedDiceRoller.AttackerWins());   // 5 dice wipe out South Africa's 5

        Assert.Equal(GameStatus.Lost, game.Status);
        Assert.Null(game.WinnerId);
        Assert.True(game.HumanPlayer.IsEliminated);
        var ended = Assert.IsType<GameEnded>(game.PendingEvents[^1]);
        Assert.Equal(GameStatus.Lost, ended.Status);
        var error = Assert.Throws<GameRuleException>(() => game.MoveAfterConquest(india, 0));
        Assert.Equal("This game is over.", error.Message);
    }

    [Fact]
    public void HumanConqueringAllTenCountries_WinsTheGame()
    {
        var game = TestGames.Create(seed: 4);
        var human = game.HumanPlayer.Id;
        // Take out whoever moves first in each round early, so later rounds wrap past an eliminated player.
        var targets = game.TurnOrder.Where(id => id != human)
            .Select(id => game.CountriesOwnedBy(id)[0].Code)
            .ToList();
        var roundsAtHumanTurns = new List<int>();

        foreach (var target in targets)
        {
            TestGames.PassUntil(game, "South Africa");
            roundsAtHumanTurns.Add(game.Round);
            TestGames.PlaceAllOn(game, "ZA");
            game.StartAttack(human, "ZA", target);
            while (game.Phase == TurnPhase.AttackInProgress)
            {
                game.Roll(human, RiggedDiceRoller.AttackerWins());
            }
            if (game.Status != GameStatus.InProgress) break;
            game.MoveAfterConquest(human, 0);
            game.EndTurn(human);
        }

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(human, game.WinnerId);
        Assert.All(game.Countries, c => Assert.Equal(human, c.OwnerId));
        Assert.Equal(9, game.Players.Count(p => p.IsEliminated));
        Assert.Equal(GameStatus.Won, Assert.IsType<GameEnded>(game.PendingEvents[^1]).Status);
        Assert.Throws<GameRuleException>(() => game.EndTurn(human));
        // The human moves once per round, even after players at the start of the turn order are eliminated.
        Assert.All(roundsAtHumanTurns.Zip(roundsAtHumanTurns.Skip(1)), pair => Assert.Equal(pair.First + 1, pair.Second));
    }

    [Fact]
    public void EliminatedPlayers_AreSkipped()
    {
        var game = TestGames.Create();
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "VA");
        game.Roll(human, new ScriptedDiceRoller(6, 1));
        game.MoveAfterConquest(human, 0);
        var vatican = game.Players.Single(p => p.Name == "Vatican City").Id;
        game.DequeueEvents();

        game.EndTurn(human);
        TestGames.PassUntil(game, "South Africa");

        var turns = game.PendingEvents.OfType<TurnStarted>().ToList();
        Assert.Equal(9, turns.Count);
        Assert.DoesNotContain(turns, t => t.PlayerId == vatican);
    }
}
