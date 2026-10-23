using Geopolitics.Domain.Games;
using Geopolitics.Infrastructure.Persistence;
using Geopolitics.Infrastructure.Persistence.Documents;
using Geopolitics.Tests.TestDoubles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Geopolitics.Tests.Infrastructure;

public class GameDocumentMapperTests
{
    // MongoDB stores dates to the millisecond, so tests use whole-millisecond creation times.
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 24, 9, 30, 15, 123, TimeSpan.Zero);

    static GameDocumentMapperTests() => MongoConventions.Register();

    private static Game MidAttackGame()
    {
        var game = Game.Create(
            Guid.NewGuid(), TestGames.OwnerUserId, TestGames.SouthAfrica, TestGames.Opponents, new Random(4), CreatedAt);
        var human = TestGames.PassUntil(game, "South Africa");
        game.StartAttack(human, "ZA", "VA");
        game.Roll(human, new ScriptedDiceRoller(6, 1));
        game.MoveAfterConquest(human, 2);
        game.EndTurn(human);
        var india = TestGames.PassUntil(game, "India");
        TestGames.PlaceAllOn(game, "IN");
        game.StartAttack(india, "IN", "ZA");
        // South Africa kept 3 soldiers, so 3 dice each: India loses 2, South Africa 1, and the attack continues.
        game.Roll(india, new ScriptedDiceRoller(6, 1, 1, /* vs */ 1, 6, 6));
        game.MarkSaved(game.PendingEvents.Count);
        return game;
    }

    /// <summary>Through the real BSON serializer, as MongoDB would store and return it.</summary>
    private static GameDocument ThroughBson(GameDocument document) =>
        BsonSerializer.Deserialize<GameDocument>(document.ToBsonDocument());

    [Fact]
    public void GameState_SurvivesDocumentAndBsonRoundTrip()
    {
        var state = MidAttackGame().ToState();

        var restored = GameDocumentMapper.ToState(ThroughBson(GameDocumentMapper.ToDocument(state, DateTime.UtcNow)));

        Assert.Equivalent(state, restored, strict: true);
        Assert.Equivalent(state, Game.Restore(restored).ToState(), strict: true);
    }

    [Fact]
    public void Document_UsesReadableFieldsAndStandardUuids()
    {
        var state = MidAttackGame().ToState();

        var bson = GameDocumentMapper.ToDocument(state, DateTime.UtcNow).ToBsonDocument();

        Assert.Equal(state.Id, bson["_id"].AsGuid);
        Assert.Equal(BsonBinarySubType.UuidStandard, bson["_id"].AsBsonBinaryData.SubType);
        Assert.Equal("AttackInProgress", bson["phase"].AsString);
        Assert.Equal("InProgress", bson["status"].AsString);
        Assert.Equal("ZA", bson["humanCountryCode"].AsString);
        Assert.Equal("IN", bson["activeAttack"]["fromCode"].AsString);
        Assert.Equal("Human", bson["players"].AsBsonArray.Single(p => p["userId"] != BsonNull.Value)["kind"].AsString);
        Assert.Equal(10, bson["countries"].AsBsonArray.Count);
        Assert.Equal(1, bson["version"].AsInt32);
    }

    [Fact]
    public void HumanCountryCode_StaysTheStartingCountryAfterItIsLost()
    {
        var game = Game.Create(
            Guid.NewGuid(), TestGames.OwnerUserId, TestGames.SouthAfrica, TestGames.Opponents, new Random(4), CreatedAt);
        var india = TestGames.PassUntil(game, "India");
        TestGames.PlaceAllOn(game, "IN");
        game.StartAttack(india, "IN", "ZA");
        game.Roll(india, RiggedDiceRoller.AttackerWins());

        var document = GameDocumentMapper.ToDocument(game.ToState(), DateTime.UtcNow);

        Assert.Equal(GameStatus.Lost, document.Status);
        Assert.Equal("ZA", document.HumanCountryCode);
    }
}
