namespace Geopolitics.Domain.Battles;

/// <summary>Rolls fair dice. Pass a seeded <see cref="Random"/> for reproducible sequences.</summary>
public sealed class RandomDiceRoller(Random? random = null) : IDiceRoller
{
    private readonly Random _random = random ?? Random.Shared;

    public IReadOnlyList<int> Roll(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = _random.Next(BattleEngine.MinDieValue, BattleEngine.MaxDieValue + 1);
        }
        return values;
    }
}
