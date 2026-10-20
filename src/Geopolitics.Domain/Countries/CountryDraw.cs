namespace Geopolitics.Domain.Countries;

/// <summary>Picks the opponents for a new game from the full country catalogue.</summary>
public static class CountryDraw
{
    public const int OpponentCount = 9;

    /// <summary>Returns <see cref="OpponentCount"/> distinct countries, chosen at random, excluding the player's.</summary>
    public static IReadOnlyList<CountryProfile> PickOpponents(
        IReadOnlyCollection<CountryProfile> catalogue, string playerCountryCode, Random random)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerCountryCode);
        ArgumentNullException.ThrowIfNull(random);

        if (!catalogue.Any(c => SameCode(c.Code, playerCountryCode)))
        {
            throw new GameRuleException($"'{playerCountryCode}' is not in the country list.");
        }

        var candidates = catalogue
            .Where(c => !SameCode(c.Code, playerCountryCode))
            .DistinctBy(c => c.Code)
            .ToArray();
        if (candidates.Length < OpponentCount)
        {
            throw new InvalidOperationException(
                $"The catalogue needs at least {OpponentCount} other countries; it has {candidates.Length}.");
        }

        random.Shuffle(candidates);
        return candidates[..OpponentCount];
    }

    private static bool SameCode(string a, string b) => string.Equals(a, b.Trim(), StringComparison.OrdinalIgnoreCase);
}
