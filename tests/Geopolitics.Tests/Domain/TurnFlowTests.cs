using Geopolitics.Domain;
using Geopolitics.Domain.Games;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class TurnFlowTests
{
    [Fact]
    public void EndTurn_PassesToNextPlayerInTurnOrder()
    {
        var game = TestGames.Create();

        game.EndTurn(game.TurnOrder[0]);

        Assert.Equal(game.TurnOrder[1], game.CurrentPlayer.Id);
        Assert.Equal(1, game.Round);
    }

    [Fact]
    public void RoundTwo_StartsOnceEveryPlayerHasHadOneTurn()
    {
        var game = TestGames.Create();

        for (var i = 0; i < 9; i++)
        {
            game.EndTurn(game.CurrentPlayer.Id);
            Assert.Equal(1, game.Round);
        }
        game.EndTurn(game.CurrentPlayer.Id);

        Assert.Equal(2, game.Round);
        Assert.Equal(game.TurnOrder[0], game.CurrentPlayer.Id);
    }

    [Fact]
    public void RoundTwo_OpensWithReinforcements()
    {
        var game = TestGames.CreateInRoundTwo();

        Assert.Equal(TurnPhase.Reinforce, game.Phase);
        Assert.Equal(2, game.PendingReinforcements);
        var started = Assert.IsType<TurnStarted>(game.PendingEvents[^1]);
        Assert.Equal((2, 2), (started.Round, started.Reinforcements));
    }

    [Fact]
    public void EndTurn_RecordsTurnEndedThenTurnStarted()
    {
        var game = TestGames.Create();
        var first = game.CurrentPlayer.Id;
        game.DequeueEvents();

        game.EndTurn(first);

        Assert.Collection(game.DequeueEvents(),
            e => Assert.Equal(first, Assert.IsType<TurnEnded>(e).PlayerId),
            e => Assert.Equal(game.CurrentPlayer.Id, Assert.IsType<TurnStarted>(e).PlayerId));
        Assert.Empty(game.PendingEvents);
    }

    [Fact]
    public void Moves_ByAPlayerWhoseTurnItIsNot_AreRejected()
    {
        var game = TestGames.Create();
        var other = game.TurnOrder[1];
        var otherCountry = game.CountriesOwnedBy(other)[0].Code;

        var error = Assert.Throws<GameRuleException>(() => game.EndTurn(other));
        Assert.Equal("It is not your turn.", error.Message);
        Assert.Throws<GameRuleException>(() => game.Transfer(other, otherCountry, otherCountry, 1));
        Assert.Equal(game.TurnOrder[0], game.CurrentPlayer.Id);
    }

    [Fact]
    public void Moves_ByAPlayerNotInTheGame_AreRejected()
    {
        var game = TestGames.Create();

        Assert.Throws<GameRuleException>(() => game.EndTurn(Guid.NewGuid()));
    }

    [Fact]
    public void EndTurn_IsRejectedUntilReinforcementsArePlaced()
    {
        var game = TestGames.CreateInRoundTwo();

        var error = Assert.Throws<GameRuleException>(() => game.EndTurn(game.CurrentPlayer.Id));

        Assert.Contains("reinforcements", error.Message);
        Assert.Equal(TurnPhase.Reinforce, game.Phase);
    }
}
