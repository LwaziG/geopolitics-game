using Geopolitics.Domain.Games;
using Geopolitics.Infrastructure.Persistence;
using Geopolitics.Tests.TestDoubles;
using MongoDB.Bson;

namespace Geopolitics.Tests.Infrastructure;

public class GameEventSerializerTests
{
    /// <summary>Every event a won game and a lost game produce, which between them covers every event type.</summary>
    private static List<GameEvent> EventsFromFullGames()
    {
        var events = new List<GameEvent>();

        var won = TestGames.Create(seed: 4);
        var human = won.HumanPlayer.Id;
        foreach (var target in TestGames.Opponents.Select(o => o.Code))
        {
            TestGames.PassUntil(won, "South Africa");
            TestGames.PlaceAllOn(won, "ZA");
            won.StartAttack(human, "ZA", target);
            while (won.Phase == TurnPhase.AttackInProgress) won.Roll(human, RiggedDiceRoller.AttackerWins());
            if (won.Status != GameStatus.InProgress) break;
            won.MoveAfterConquest(human, 1);
            won.Transfer(human, target, "ZA", 1);
            won.EndTurn(human);
        }
        events.AddRange(won.DequeueEvents());

        var lost = TestGames.Create(seed: 4);
        var india = TestGames.PassUntil(lost, "India");
        TestGames.PlaceAllOn(lost, "IN");
        lost.StartAttack(india, "IN", "VA");
        lost.StopAttack(india);
        var za = TestGames.PassUntil(lost, "South Africa");
        TestGames.PlaceAllOn(lost, "ZA");
        lost.StartAttack(za, "ZA", "IN");
        lost.Roll(za, RiggedDiceRoller.DefenderWins());   // AttackFailed
        events.AddRange(lost.DequeueEvents());

        return events;
    }

    [Fact]
    public void EveryEventType_SurvivesARoundTrip()
    {
        var events = EventsFromFullGames();
        var allTypes = typeof(GameEvent).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract)
            .Select(t => t.Name)
            .Order();
        Assert.Equal(allTypes, events.Select(e => e.GetType().Name).Distinct().Order());

        foreach (var original in events)
        {
            var (type, data) = GameEventSerializer.Serialize(original);
            var restored = GameEventSerializer.Deserialize(type, data);

            Assert.IsType(original.GetType(), restored);
            Assert.Equivalent(original, restored, strict: true);
        }
    }

    [Fact]
    public void StoredData_IsReadable()
    {
        var gameId = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var rolled = new DiceRolled(gameId, attacker, "ZA", "IN", [1, 4], [2, 3], 1, 1, 9, 1);

        var (type, data) = GameEventSerializer.Serialize(rolled);
        var (_, ended) = GameEventSerializer.Serialize(new GameEnded(gameId, GameStatus.Won, attacker));

        Assert.Equal("DiceRolled", type);
        Assert.Equal(new BsonArray { 1, 4 }, data["attackerDice"]);
        Assert.Equal("ZA", data["fromCode"].AsString);
        Assert.Equal(attacker.ToString(), data["attackerId"].AsString);
        Assert.Equal("Won", ended["status"].AsString);
    }

    [Fact]
    public void Deserialize_RejectsUnknownTypes()
    {
        Assert.Throws<InvalidOperationException>(() => GameEventSerializer.Deserialize("Teleported", []));
    }
}
