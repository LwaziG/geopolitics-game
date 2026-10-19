using Geopolitics.Domain.Battles;

namespace Geopolitics.Tests.TestDoubles;

/// <summary>Returns pre-set die values in order, so tests control every battle outcome.</summary>
public sealed class ScriptedDiceRoller(params int[] values) : IDiceRoller
{
    private readonly Queue<int> _values = new(values);

    public int Remaining => _values.Count;

    public IReadOnlyList<int> Roll(int count)
    {
        if (count > _values.Count)
        {
            throw new InvalidOperationException(
                $"Script asked for {count} dice but only {_values.Count} remain.");
        }
        return [.. Enumerable.Range(0, count).Select(_ => _values.Dequeue())];
    }
}
