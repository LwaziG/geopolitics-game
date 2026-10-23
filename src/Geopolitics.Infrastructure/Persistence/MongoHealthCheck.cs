using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>
/// Healthy when the database answers a ping and the country list is loaded; degraded (still serving) when
/// the countries are missing, because no new game can be started.
/// </summary>
internal sealed class MongoHealthCheck(IMongoDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            var countries = await database.GetCollection<BsonDocument>(MongoCollections.Countries)
                .EstimatedDocumentCountAsync(cancellationToken: cancellationToken);

            var description = $"Connected to '{database.DatabaseNamespace.DatabaseName}' with {countries} countries.";
            return countries > 0 ? HealthCheckResult.Healthy(description) : HealthCheckResult.Degraded(description);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB ping failed.", ex);
        }
    }
}
