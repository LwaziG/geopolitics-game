using System.Globalization;
using Geopolitics.Application.Countries;
using Geopolitics.Domain.Countries;
using Geopolitics.Infrastructure.Persistence;
using Geopolitics.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Geopolitics.Infrastructure.Countries;

/// <summary>
/// Reads the playable countries once and keeps them in memory: the list only changes when the seed file
/// does, which means a new deployment.
/// </summary>
internal sealed class MongoCountryCatalogue(IMongoDatabase database) : ICountryCatalogue
{
    private readonly IMongoCollection<CountryDocument> _countries =
        database.GetCollection<CountryDocument>(MongoCollections.Countries);

    private volatile IReadOnlyList<CountryProfile>? _cache;

    public async Task<IReadOnlyList<CountryProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is { } cached) return cached;

        var documents = await _countries.Find(FilterDefinition<CountryDocument>.Empty).ToListAsync(cancellationToken);
        IReadOnlyList<CountryProfile> countries =
        [
            .. documents
                .Select(d => new CountryProfile(d.Code, d.Name, d.Population))
                .OrderBy(c => c.Name, StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true)),
        ];

        // Don't cache an empty list read before the seeder has run.
        if (countries.Count > 0) _cache = countries;
        return countries;
    }

    public async Task<CountryProfile?> FindAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var countries = await GetAllAsync(cancellationToken);
        return countries.FirstOrDefault(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
