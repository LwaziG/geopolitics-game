using Geopolitics.Domain.Battles;
using Geopolitics.Domain.Games;
using Geopolitics.Tests.TestDoubles;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Geopolitics.Tests.Domain;

/// <summary>
/// Plays hundreds of complete games with random legal moves and checks the rules' invariants after
/// every single move. Catches the interactions that hand-written examples miss.
/// </summary>
public class GameSimulationTests(ITestOutputHelper output)
{
    private const int Games = 500;
    private const int MaxRounds = 2_000;

    [Fact]
    public void RandomGames_KeepEveryInvariant_AndAlwaysFinish()
    {
        var outcomes = new Dictionary<GameStatus, int>();
        var rounds = new List<int>();

        for (var seed = 0; seed < Games; seed++)
        {
            var game = TestGames.Create(seed);
            try
            {
                Play(game, seed);
            }
            catch (Exception ex) when (ex is not XunitException { Message: var m } || !m.StartsWith("Seed"))
            {
                throw new XunitException($"Seed {seed}, round {game.Round}, {game.CurrentPlayer.Name}'s turn: {ex.Message}");
            }
            outcomes[game.Status] = outcomes.GetValueOrDefault(game.Status) + 1;
            rounds.Add(game.Round);
        }

        output.WriteLine($"Won {outcomes.GetValueOrDefault(GameStatus.Won)}, lost {outcomes.GetValueOrDefault(GameStatus.Lost)}; " +
                         $"rounds min {rounds.Min()}, median {rounds.Order().ElementAt(rounds.Count / 2)}, max {rounds.Max()}");
        Assert.Equal(0, outcomes.GetValueOrDefault(GameStatus.InProgress));
        Assert.True(outcomes.GetValueOrDefault(GameStatus.Won) > 0, "No simulated game was won.");
        Assert.True(outcomes.GetValueOrDefault(GameStatus.Lost) > 0, "No simulated game was lost.");
    }

    private static void Play(Game game, int seed)
    {
        var player = new RandomPlayer(new Random(seed), new RandomDiceRoller(new Random(seed + 1_000_000)));
        var replay = new EventReplay();
        AssertInvariants(game, replay);

        while (game.Status == GameStatus.InProgress)
        {
            if (game.Round > MaxRounds)
            {
                throw new XunitException($"Seed {seed}: game still running after {MaxRounds} rounds.");
            }
            player.PlayTurn(game, () => AssertInvariants(game, replay));
        }
    }

    /// <summary>
    /// Value comparison of two snapshots. The element records compare by value, but the lists holding them
    /// compare by reference, so compare the lists separately and the scalars with the lists blanked out.
    /// Much faster than Assert.Equivalent, which matters over ~160,000 moves.
    /// </summary>
    private static bool SameState(GameState a, GameState b) =>
        Scalars(a) == Scalars(b)
        && a.TurnOrder.SequenceEqual(b.TurnOrder)
        && a.Players.SequenceEqual(b.Players)
        && a.Countries.SequenceEqual(b.Countries);

    private static GameState Scalars(GameState s) =>
        s with { TurnOrder = Array.Empty<Guid>(), Players = Array.Empty<PlayerState>(), Countries = Array.Empty<CountryState>() };

    private static void AssertInvariants(Game game, EventReplay replay)
    {
        // Every change was recorded: the event log alone rebuilds the same board.
        replay.Apply(game.DequeueEvents());
        replay.AssertMatches(game);

        // Every reachable position can be saved and reloaded without loss.
        var state = game.ToState();
        Assert.True(SameState(state, Game.Restore(state).ToState()), "Restoring the saved state changed the game.");

        Assert.Equal(Game.CountriesPerGame, game.Countries.Count);
        Assert.All(game.Countries, c =>
        {
            Assert.True(c.Soldiers >= 1, $"{c.Name} has {c.Soldiers} soldiers.");
            Assert.Contains(game.Players, p => p.Id == c.OwnerId && !p.IsEliminated);
        });
        Assert.All(game.Players, p =>
        {
            var owned = game.CountriesOwnedBy(p.Id).Count;
            Assert.True(p.IsEliminated ? owned == 0 : owned >= 1, $"{p.Name} (eliminated: {p.IsEliminated}) owns {owned}.");
        });

        switch (game.Status)
        {
            case GameStatus.InProgress:
                Assert.False(game.CurrentPlayer.IsEliminated);
                Assert.Equal(game.Phase == TurnPhase.Reinforce, game.PendingReinforcements > 0);
                Assert.Equal(game.Phase is TurnPhase.AttackInProgress or TurnPhase.PostConquest, game.ActiveAttack is not null);
                if (game.ActiveAttack is { } attack)
                {
                    Assert.True(game.HasAttackedThisTurn);
                    Assert.True(game.GetCountry(attack.FromCode).IsOwnedBy(game.CurrentPlayer.Id));
                }
                break;
            case GameStatus.Won:
                Assert.Equal(game.HumanPlayer.Id, game.WinnerId);
                Assert.All(game.Countries, c => Assert.True(c.IsOwnedBy(game.HumanPlayer.Id)));
                break;
            case GameStatus.Lost:
                Assert.True(game.HumanPlayer.IsEliminated);
                Assert.Null(game.WinnerId);
                break;
        }
    }
}
