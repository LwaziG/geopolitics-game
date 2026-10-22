using Geopolitics.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>
/// Creates the indexes at startup so a fresh database sets itself up. Creating an index that already exists
/// is a no-op. A failure is logged rather than stopping the app; /health reports the database state.
/// </summary>
internal sealed class MongoIndexInitializer(IMongoDatabase database, ILogger<MongoIndexInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateIndexesAsync(database, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not create MongoDB indexes.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static async Task CreateIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var games = database.GetCollection<GameDocument>(MongoCollections.Games);
        var gameKeys = Builders<GameDocument>.IndexKeys;
        await games.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<GameDocument>(
                    gameKeys.Ascending(g => g.OwnerUserId).Descending(g => g.UpdatedAt),
                    new CreateIndexOptions { Name = "owner_updated" }),
                new CreateIndexModel<GameDocument>(
                    gameKeys.Ascending(g => g.Status).Descending(g => g.UpdatedAt),
                    new CreateIndexOptions { Name = "status_updated" }),
            ],
            cancellationToken);

        var events = database.GetCollection<GameEventDocument>(MongoCollections.GameEvents);
        await events.Indexes.CreateOneAsync(
            new CreateIndexModel<GameEventDocument>(
                Builders<GameEventDocument>.IndexKeys.Ascending(e => e.GameId).Ascending(e => e.Seq),
                new CreateIndexOptions { Name = "game_seq", Unique = true }),
            cancellationToken: cancellationToken);

        var countries = database.GetCollection<BsonDocument>(MongoCollections.Countries);
        await countries.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("name"),
                new CreateIndexOptions { Name = "name" }),
            cancellationToken: cancellationToken);
    }
}
