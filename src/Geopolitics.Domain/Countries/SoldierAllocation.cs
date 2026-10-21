namespace Geopolitics.Domain.Countries;

/// <summary>Maps a country's population to its starting soldier value, using the brief's bands.</summary>
public static class SoldierAllocation
{
    public const int MinSoldierValue = 1;
    public const int MaxSoldierValue = 9;

    // Upper limits are inclusive: "up to 5 million" means population <= 5,000,000.
    private static readonly (long UpTo, int Soldiers)[] Bands =
    [
        (5_000_000, 1),
        (10_000_000, 2),
        (25_000_000, 3),
        (50_000_000, 4),
        (75_000_000, 5),
        (100_000_000, 6),
        (125_000_000, 7),
        (150_000_000, 8),
    ];

    public static int FromPopulation(long population)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(population);

        foreach (var (upTo, soldiers) in Bands)
        {
            if (population <= upTo) return soldiers;
        }
        return MaxSoldierValue;
    }
}
