using Geopolitics.Domain.Battles;

namespace Geopolitics.Tests.TestDoubles;

/// <summary>
/// Makes one side win every pair, whatever the dice count. Relies on the battle engine rolling
/// the attacker's dice first and the defender's second.
/// </summary>
public sealed class RiggedDiceRoller : IDiceRoller
{
    private readonly int _attackerValue;
    private readonly int _defenderValue;
    private bool _attackerNext = true;

    private RiggedDiceRoller(int attackerValue, int defenderValue)
    {
        _attackerValue = attackerValue;
        _defenderValue = defenderValue;
    }

    public static RiggedDiceRoller AttackerWins() => new(6, 1);

    public static RiggedDiceRoller DefenderWins() => new(1, 6);

    public IReadOnlyList<int> Roll(int count)
    {
        var value = _attackerNext ? _attackerValue : _defenderValue;
        _attackerNext = !_attackerNext;
        return Enumerable.Repeat(value, count).ToArray();
    }
}
