namespace Geopolitics.Domain.Countries;

/// <summary>Reference data for a real-world country: what a game is set up from.</summary>
public sealed record CountryProfile
{
    public CountryProfile(string code, string name, long population)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(population);

        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Population = population;
    }

    /// <summary>ISO 3166-1 alpha-2 code, upper case (e.g. "ZA").</summary>
    public string Code { get; }

    public string Name { get; }

    public long Population { get; }

    public int SoldierValue => SoldierAllocation.FromPopulation(Population);
}
