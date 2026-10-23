using System.Text.Json;
using Geopolitics.Domain.Countries;

namespace Geopolitics.Infrastructure.Countries;

/// <summary>
/// The playable countries, captured once from Worldometers by <c>tools/countries/build_countries.py</c>
/// and embedded in this assembly as <c>countries.json</c>.
/// </summary>
internal sealed record CountrySeedFile(
    string Source,
    DateOnly CapturedOn,
    int? PopulationYear,
    IReadOnlyList<CountrySeedFile.Entry> Countries)
{
    private const string ResourceName = "Geopolitics.Infrastructure.Countries.countries.json";

    private static readonly Lazy<CountrySeedFile> Embedded = new(Load);

    public sealed record Entry(string Code, string Name, long Population);

    public static CountrySeedFile Current => Embedded.Value;

    public IReadOnlyList<CountryProfile> ToProfiles() =>
        [.. Countries.Select(c => new CountryProfile(c.Code, c.Name, c.Population))];

    private static CountrySeedFile Load()
    {
        using var stream = typeof(CountrySeedFile).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        return JsonSerializer.Deserialize<CountrySeedFile>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("countries.json is empty.");
    }
}
