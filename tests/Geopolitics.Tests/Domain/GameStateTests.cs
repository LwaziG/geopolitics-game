using Geopolitics.Domain.Games;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class GameStateTests
{
    public static TheoryData<string> Positions => ["start", "roundTwoReinforce", "attackInProgress", "postConquest", "won", "lost"];

    /// <summary>Builds games in each phase a game can be saved in.</summary>
    private static Game At(string position)
    {
        var game = TestGames.Create(seed: 4);
        Guid human;
        switch (position)
        {
            case "start":
                return game;
            case "roundTwoReinforce":
                return TestGames.CreateInRoundTwo();
            case "attackInProgress":
                human = TestGames.PassUntil(game, "South Africa");
                game.StartAttack(human, "ZA", "IN");
                game.Roll(human, new ScriptedDiceRoller(6, 6, 1, 1, /* vs */ 1, 1, 6, 6));
                return game;
            case "postConquest":
                human = TestGames.PassUntil(game, "South Africa");
                game.StartAttack(human, "ZA", "VA");
                game.Roll(human, new ScriptedDiceRoller(6, 1));
                return game;
            case "won":
                human = game.HumanPlayer.Id;
                foreach (var target in TestGames.Opponents.Select(o => o.Code))
                {
                    TestGames.PassUntil(game, "South Africa");
                    TestGames.PlaceAllOn(game, "ZA");
                    game.StartAttack(human, "ZA", target);
                    while (game.Phase == TurnPhase.AttackInProgress) game.Roll(human, RiggedDiceRoller.AttackerWins());
                    if (game.Status != GameStatus.InProgress) break;
                    game.MoveAfterConquest(human, 0);
                    game.EndTurn(human);
                }
                return game;
            case "lost":
                var india = TestGames.PassUntil(game, "India");
                TestGames.PlaceAllOn(game, "IN");
                game.StartAttack(india, "IN", "ZA");
                game.Roll(india, RiggedDiceRoller.AttackerWins());
                return game;
            default:
                throw new ArgumentOutOfRangeException(nameof(position));
        }
    }

    [Theory]
    [MemberData(nameof(Positions))]
    public void RestoreOfToState_GivesAnIdenticalGame(string position)
    {
        var original = At(position);

        var restored = Game.Restore(original.ToState());

        Assert.Equivalent(original.ToState(), restored.ToState(), strict: true);
        Assert.Empty(restored.PendingEvents);
    }

    [Fact]
    public void RestoredGame_PlaysOnExactlyLikeTheOriginal()
    {
        var original = At("attackInProgress");
        var restored = Game.Restore(original.ToState());
        var human = original.HumanPlayer.Id;

        foreach (var game in new[] { original, restored })
        {
            game.Roll(human, new ScriptedDiceRoller(6, 6, /* vs */ 1, 2));
            game.StopAttack(human);
        }

        Assert.Equivalent(original.ToState(), restored.ToState(), strict: true);
    }

    [Fact]
    public void ToState_IsADetachedCopy()
    {
        var game = TestGames.Create();
        var state = game.ToState();

        game.EndTurn(game.CurrentPlayer.Id);

        Assert.NotEqual(state.CurrentPlayerId, game.CurrentPlayer.Id);
        Assert.Equal(game.TurnOrder[0], state.CurrentPlayerId);
    }

    [Fact]
    public void MarkSaved_BumpsVersionAndDropsOnlyTheSavedEvents()
    {
        var game = TestGames.Create();
        var saved = game.PendingEvents.Count;
        game.EndTurn(game.CurrentPlayer.Id);
        var total = game.PendingEvents.Count;

        game.MarkSaved(saved);

        Assert.Equal(1, game.Version);
        Assert.Equal(saved, game.LastEventSeq);
        Assert.Equal(total - saved, game.PendingEvents.Count);
        Assert.IsType<TurnEnded>(game.PendingEvents[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => game.MarkSaved(total));
    }

    [Fact]
    public void VersionAndEventSequence_SurviveARoundTrip()
    {
        var game = TestGames.Create();
        game.MarkSaved(game.PendingEvents.Count);

        var restored = Game.Restore(game.ToState());

        Assert.Equal((1, 2L), (restored.Version, restored.LastEventSeq));
    }

    public static TheoryData<string, Func<GameState, GameState>> Corruptions => new()
    {
        { "at least 1", s => s with { Countries = Replace(s.Countries, 0, c => c with { Soldiers = 0 }) } },
        { "not in the game", s => s with { Countries = Replace(s.Countries, 0, c => c with { OwnerId = Guid.NewGuid() }) } },
        { "does not match its population", s => s with { Countries = Replace(s.Countries, 0, c => c with { SoldierValue = 9 }) } },
        { "codes are not unique", s => s with { Countries = Replace(s.Countries, 1, c => c with { Code = s.Countries[0].Code }) } },
        { "expected 10 countries", s => s with { Countries = s.Countries.Skip(1).ToList() } },
        { "turn order", s => s with { TurnOrder = [.. s.TurnOrder.Skip(1), Guid.NewGuid()] } },
        { "exactly one human", s => s with { Players = s.Players.Select(p => p with { Kind = Geopolitics.Domain.Players.PlayerKind.Human, UserId = s.OwnerUserId }).ToList() } },
        { "not linked to the game's owner", s => s with { OwnerUserId = "someone-else" } },
        { "eliminated but still owns", s => s with { Players = Replace(s.Players, 0, p => p with { IsEliminated = true }) } },
        { "Reinforce", s => s with { Phase = TurnPhase.Reinforce } },
        { "does not match the attack state", s => s with { Phase = TurnPhase.AttackInProgress } },
        { "cannot have a winner", s => s with { WinnerId = s.Players[0].Id } },
        { "must have the human as winner", s => s with { Status = GameStatus.Won } },
        { "round must be at least 1", s => s with { Round = 0 } },
    };

    [Theory]
    [MemberData(nameof(Corruptions))]
    public void Restore_RejectsCorruptedSnapshots(string expectedProblem, Func<GameState, GameState> corrupt)
    {
        var state = corrupt(TestGames.Create().ToState());

        var error = Assert.Throws<ArgumentException>(() => Game.Restore(state));

        Assert.Contains(expectedProblem, error.Message);
    }

    private static List<T> Replace<T>(IReadOnlyList<T> items, int index, Func<T, T> change) =>
        [.. items.Select((item, i) => i == index ? change(item) : item)];
}
