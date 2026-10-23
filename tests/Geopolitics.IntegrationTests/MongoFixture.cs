using Geopolitics.Infrastructure.Persistence;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Geopolitics.IntegrationTests;

/// <summary>
/// One real MongoDB for the whole test run, started in a container (Rancher Desktop or Docker) as a
/// single-node replica set so transactions work like they do on Atlas. Each test gets its own database.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0")
        .WithReplicaSet()
        .Build();

    public IMongoClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        MongoConventions.Register();
        await _container.StartAsync();
        Client = new MongoClient(_container.GetConnectionString());
    }

    /// <summary>A fresh, empty database with the app's indexes.</summary>
    public async Task<IMongoDatabase> NewDatabaseAsync()
    {
        var database = Client.GetDatabase($"test_{Guid.NewGuid():N}");
        await DatabaseInitializer.CreateIndexesAsync(database, CancellationToken.None);
        return database;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "MongoDB";
}
