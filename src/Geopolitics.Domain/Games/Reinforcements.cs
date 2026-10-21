namespace Geopolitics.Domain.Games;

public static class Reinforcements
{
    public const int PerCountry = 2;

    /// <summary>The first round with reinforcements; round 1 has none.</summary>
    public const int FirstRound = 2;

    /// <summary>Soldiers received at the start of a turn: 2 per country controlled.</summary>
    public static int For(int countriesControlled)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(countriesControlled, 1);
        return countriesControlled * PerCountry;
    }
}
