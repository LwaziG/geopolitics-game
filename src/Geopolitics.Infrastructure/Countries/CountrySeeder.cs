using Geopolitics.Domain.Countries;
using Geopolitics.Infrastructure.Persistence;
using Geopolitics.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Geopolitics.Infrastructure.Countries;

/// <summary>
/// Makes the <c>countries</c> collection match the seed file: adds missing countries, fixes changed ones and
/// removes ones that are no longer playable. Writes nothing when it already matches, so it is safe on every start.
/// </summary>
internal static class CountrySeeder
{
    public sealed record Result(int Upserted, int Deleted)
    {
        public bool Changed => Upserted > 0 || Deleted > 0;
    }

    public static async Task<Result> SeedAsync(
        IMongoDatabase database, CountrySeedFile seed, CancellationToken cancellationToken = default)
    {
        var collection = database.GetCollection<CountryDocument>(MongoCollections.Countries);
        var capturedOn = seed.CapturedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var wanted = seed.ToProfiles()
            .Select(p => new CountryDocument
            {
                Code = p.Code,
                Name = p.Name,
                Population = p.Population,
                SoldierValue = SoldierAllocation.FromPopulation(p.Population),
                SourceCapturedOn = capturedOn,
            })
            .ToDictionary(d => d.Code, StringComparer.Ordinal);

        var existing = (await collection.Find(FilterDefinition<CountryDocument>.Empty).ToListAsync(cancellationToken))
            .ToDictionary(d => d.Code, StringComparer.Ordinal);

        var writes = new List<WriteModel<CountryDocument>>();
        foreach (var document in wanted.Values)
        {
            if (!existing.TryGetValue(document.Code, out var current) || !current.SameAs(document))
            {
                writes.Add(new ReplaceOneModel<CountryDocument>(
                    Builders<CountryDocument>.Filter.Eq(d => d.Code, document.Code), document) { IsUpsert = true });
            }
        }
        var upserted = writes.Count;

        var removed = existing.Keys.Where(code => !wanted.ContainsKey(code)).ToList();
        if (removed.Count > 0)
        {
            writes.Add(new DeleteManyModel<CountryDocument>(Builders<CountryDocument>.Filter.In(d => d.Code, removed)));
        }

        if (writes.Count > 0)
        {
            await collection.BulkWriteAsync(writes, cancellationToken: cancellationToken);
        }
        return new Result(upserted, removed.Count);
    }
}
