namespace Geopolitics.Domain.Countries;

/// <summary>
/// A country on the board of one game. Only the <c>Game</c> aggregate changes it,
/// so every change goes through the game's rule checks.
/// </summary>
public sealed class Country
{
    internal Country(CountryProfile profile, Guid ownerId)
    {
        Code = profile.Code;
        Name = profile.Name;
        Population = profile.Population;
        SoldierValue = profile.SoldierValue;
        OwnerId = ownerId;
        Soldiers = SoldierValue;
    }

    /// <summary>Rebuilds a country from a validated snapshot.</summary>
    internal Country(string code, string name, long population, int soldierValue, Guid ownerId, int soldiers)
    {
        Code = code;
        Name = name;
        Population = population;
        SoldierValue = soldierValue;
        OwnerId = ownerId;
        Soldiers = soldiers;
    }

    public string Code { get; }

    public string Name { get; }

    public long Population { get; }

    /// <summary>Population-based value: the starting soldiers and the basis of the conquest bonus.</summary>
    public int SoldierValue { get; }

    public Guid OwnerId { get; private set; }

    public int Soldiers { get; private set; }

    public bool IsOwnedBy(Guid playerId) => OwnerId == playerId;

    internal void AddSoldiers(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Soldiers += count;
    }

    internal void RemoveSoldiers(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Soldiers);
        Soldiers -= count;
    }

    /// <summary>Hands a defeated (empty) country to its conqueror with the conquest bonus.</summary>
    internal void Capture(Guid newOwnerId, int bonusSoldiers)
    {
        if (Soldiers != 0)
        {
            throw new InvalidOperationException($"{Name} still has soldiers and cannot be captured.");
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(bonusSoldiers, 1);

        OwnerId = newOwnerId;
        Soldiers = bonusSoldiers;
    }
}
