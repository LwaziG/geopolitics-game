using Geopolitics.Application.Games;
using Geopolitics.Domain.Games;
using Geopolitics.Infrastructure.Persistence;
using Geopolitics.Infrastructure.Persistence.Documents;
using Geopolitics.Tests.TestDoubles;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Geopolitics.IntegrationTests;

[Collection(MongoCollection.Name)]
public class GameRepositoryTests(MongoFixture mongo)
{
    // MongoDB stores dates to the millisecond, so games are created on a whole millisecond.
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 25, 10, 0, 0, 250, TimeSpan.Zero);

    private async Task<(MongoGameRepository Repository, IMongoDatabase Database)> NewRepositoryAsync()
    {
        var database = await mongo.NewDatabaseAsync();
        return (new MongoGameRepository(mongo.Client, database, TimeProvider.System), database);
    }

    private static Game NewGame() =>
        Game.Create(Guid.NewGuid(), TestGames.OwnerUserId, TestGames.SouthAfrica, TestGames.Opponents, new Random(4), CreatedAt);

    [Fact]
    public async Task Add_ThenGet_ReturnsTheSameGame()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();
        var eventCount = game.PendingEvents.Count;

        await repository.AddAsync(game);
        var loaded = await repository.GetAsync(game.Id);

        Assert.NotNull(loaded);
        Assert.Equivalent(game.ToState(), loaded.ToState(), strict: true);
        Assert.Equal((1, (long)eventCount), (loaded.Version, loaded.LastEventSeq));
        Assert.Empty(game.PendingEvents);
    }

    [Fact]
    public async Task Add_StoresTheEventsInOrder()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();

        await repository.AddAsync(game);
        var events = await repository.GetEventsAsync(game.Id);

        Assert.Equal([1L, 2L], events.Select(e => e.Seq));
        var started = Assert.IsType<GameStarted>(events[0].Event);
        Assert.Equal(10, started.Countries.Count);
        Assert.IsType<TurnStarted>(events[1].Event);
        Assert.All(events, e => Assert.Null(e.Commentary));
    }

    [Fact]
    public async Task Save_AppendsNewEventsAndBumpsTheVersion()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();
        await repository.AddAsync(game);

        var loaded = (await repository.GetAsync(game.Id))!;
        loaded.EndTurn(loaded.CurrentPlayer.Id);
        await repository.SaveAsync(loaded);
        var reloaded = (await repository.GetAsync(game.Id))!;

        Assert.Equal((2, 4L), (reloaded.Version, reloaded.LastEventSeq));
        Assert.Equal(loaded.CurrentPlayer.Id, reloaded.CurrentPlayer.Id);
        var newEvents = await repository.GetEventsAsync(game.Id, afterSeq: 2);
        Assert.Equal([3L, 4L], newEvents.Select(e => e.Seq));
        Assert.IsType<TurnEnded>(newEvents[0].Event);
        Assert.IsType<TurnStarted>(newEvents[1].Event);
    }

    [Fact]
    public async Task EventLog_ReplaysToTheStoredBoard()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();
        await repository.AddAsync(game);

        // A conquest, a post-conquest move, a transfer and an attack by an NPC, saving after each move.
        var human = TestGames.PassUntil(game, "South Africa");
        await repository.SaveAsync(game);
        game.StartAttack(human, "ZA", "VA");
        game.Roll(human, new ScriptedDiceRoller(6, 1));
        await repository.SaveAsync(game);
        game.MoveAfterConquest(human, 1);
        game.Transfer(human, "VA", "ZA", 1);
        game.EndTurn(human);
        await repository.SaveAsync(game);
        var india = TestGames.PassUntil(game, "India");
        TestGames.PlaceAllOn(game, "IN");
        game.StartAttack(india, "IN", "VA");
        game.Roll(india, RiggedDiceRoller.AttackerWins());
        await repository.SaveAsync(game);

        var stored = (await repository.GetAsync(game.Id))!;
        var events = await repository.GetEventsAsync(game.Id);
        var replay = new EventReplay();
        replay.Apply(events.Select(e => e.Event));

        replay.AssertMatches(stored);
        Assert.Equal(Enumerable.Range(1, events.Count).Select(i => (long)i), events.Select(e => e.Seq));
        Assert.Equal(stored.LastEventSeq, events.Count);
    }

    [Fact]
    public async Task StaleSave_IsRejectedAndWritesNothing()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();
        await repository.AddAsync(game);
        var first = (await repository.GetAsync(game.Id))!;
        var second = (await repository.GetAsync(game.Id))!;

        first.EndTurn(first.CurrentPlayer.Id);
        await repository.SaveAsync(first);
        second.EndTurn(second.CurrentPlayer.Id);
        second.EndTurn(second.CurrentPlayer.Id);

        var error = await Assert.ThrowsAsync<ConcurrencyException>(() => repository.SaveAsync(second));

        Assert.Equal((game.Id, 1), (error.GameId, error.ExpectedVersion));
        var stored = (await repository.GetAsync(game.Id))!;
        Assert.Equivalent(first.ToState(), stored.ToState(), strict: true);
        Assert.Equal(4, (await repository.GetEventsAsync(game.Id)).Count);
        Assert.NotEmpty(second.PendingEvents);   // nothing was marked saved
    }

    [Fact]
    public async Task StaleSaveWithNoNewEvents_IsRejectedByTheVersionCheck()
    {
        // With new events, the unique (gameId, seq) index also stops a stale save. Without any, only the
        // version check stands between an old copy and overwriting newer moves.
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();
        await repository.AddAsync(game);
        var first = (await repository.GetAsync(game.Id))!;
        var stale = (await repository.GetAsync(game.Id))!;
        first.EndTurn(first.CurrentPlayer.Id);
        await repository.SaveAsync(first);

        await Assert.ThrowsAsync<ConcurrencyException>(() => repository.SaveAsync(stale));

        var stored = (await repository.GetAsync(game.Id))!;
        Assert.Equal(first.CurrentPlayer.Id, stored.CurrentPlayer.Id);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task ConcurrentSaves_ExactlyOneWins()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();
        await repository.AddAsync(game);
        var copies = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => repository.GetAsync(game.Id)));
        foreach (var copy in copies) copy!.EndTurn(copy.CurrentPlayer.Id);

        var results = await Task.WhenAll(copies.Select(async copy =>
        {
            try
            {
                await repository.SaveAsync(copy!);
                return "saved";
            }
            catch (ConcurrencyException)
            {
                return "rejected";
            }
        }));

        Assert.Equal(1, results.Count(r => r == "saved"));
        Assert.Equal(3, results.Count(r => r == "rejected"));
        var stored = (await repository.GetAsync(game.Id))!;
        Assert.Equal(2, stored.Version);
        Assert.Equal([1L, 2L, 3L, 4L], (await repository.GetEventsAsync(game.Id)).Select(e => e.Seq));
    }

    [Fact]
    public async Task AddingTheSameGameTwice_IsRejected()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();
        var duplicate = Game.Restore(game.ToState());
        await repository.AddAsync(game);

        await Assert.ThrowsAsync<ConcurrencyException>(() => repository.AddAsync(duplicate));
        Assert.Equal(2, (await repository.GetEventsAsync(game.Id)).Count);
    }

    [Fact]
    public async Task AddAndSave_RejectTheWrongLifecycleStage()
    {
        var (repository, _) = await NewRepositoryAsync();
        var game = NewGame();

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(game));
        await repository.AddAsync(game);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AddAsync(game));
    }

    [Fact]
    public async Task Get_UnknownGame_ReturnsNull()
    {
        var (repository, _) = await NewRepositoryAsync();

        Assert.Null(await repository.GetAsync(Guid.NewGuid()));
        Assert.Empty(await repository.GetEventsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Get_RejectsACorruptedDocument()
    {
        var (repository, database) = await NewRepositoryAsync();
        var game = NewGame();
        await repository.AddAsync(game);
        await database.GetCollection<GameDocument>(MongoCollections.Games).UpdateOneAsync(
            g => g.Id == game.Id, Builders<GameDocument>.Update.Set("countries.0.soldiers", 0));

        var error = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(game.Id));

        Assert.Contains("at least 1", error.Message);
    }

    [Fact]
    public async Task Indexes_AreCreated()
    {
        var (_, database) = await NewRepositoryAsync();

        async Task<List<BsonDocument>> IndexesOf(string collection) =>
            await (await database.GetCollection<BsonDocument>(collection).Indexes.ListAsync()).ToListAsync();

        var gameIndexes = (await IndexesOf(MongoCollections.Games)).Select(i => i["name"].AsString);
        var eventIndexes = await IndexesOf(MongoCollections.GameEvents);
        var countryIndexes = (await IndexesOf(MongoCollections.Countries)).Select(i => i["name"].AsString);

        Assert.Contains("owner_updated", gameIndexes);
        Assert.Contains("status_updated", gameIndexes);
        Assert.Contains("name", countryIndexes);
        var seq = Assert.Single(eventIndexes, i => i["name"] == "game_seq");
        Assert.True(seq["unique"].AsBoolean);
    }
}
