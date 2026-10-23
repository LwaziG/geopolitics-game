using MongoDB.Bson.Serialization.Attributes;

namespace Geopolitics.Infrastructure.Persistence.Documents;

/// <summary>A playable country in the <c>countries</c> collection, keyed by ISO alpha-2 code.</summary>
internal sealed class CountryDocument
{
    [BsonId]
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long Population { get; set; }

    /// <summary>Derived from population; stored so the data is readable without the rules.</summary>
    public int SoldierValue { get; set; }

    /// <summary>When the population figures were captured from the source.</summary>
    public DateTime SourceCapturedOn { get; set; }

    public bool SameAs(CountryDocument other) =>
        (Code, Name, Population, SoldierValue, SourceCapturedOn)
        == (other.Code, other.Name, other.Population, other.SoldierValue, other.SourceCapturedOn);
}
