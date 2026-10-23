using Geopolitics.Infrastructure.Countries;
using Geopolitics.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>
/// Sets up the database at startup so a fresh one works without manual steps: creates the indexes (a no-op
/// when they exist) and brings the country list in line with the seed file. A failure is logged rather than
/// stopping the app; /health reports the database state.
/// </summary>
internal sealed class DatabaseInitializer(IMongoDatabase database, ILogger<DatabaseInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateIndexesAsync(database, cancellationToken);
            var seeded = await CountrySeeder.SeedAsync(database, CountrySeedFile.Current, cancellationToken);
            if (seeded.Changed)
            {
                logger.LogInformation(
                    "Country list updated: {Upserted} added or changed, {Deleted} removed.", seeded.Upserted, seeded.Deleted);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not initialise the MongoDB database.");
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

        var countries = database.GetCollection<CountryDocument>(MongoCollections.Countries);
        await countries.Indexes.CreateOneAsync(
            new CreateIndexModel<CountryDocument>(
                Builders<CountryDocument>.IndexKeys.Ascending(c => c.Name),
                new CreateIndexOptions { Name = "name" }),
            cancellationToken: cancellationToken);
    }
}
