using Geopolitics.Domain.Countries;

namespace Geopolitics.Domain.Battles;

/// <summary>
/// Pure battle rules from the brief. Callers (the <c>Game</c> aggregate) check turn and ownership
/// rules first; these methods only guard against impossible arithmetic.
/// </summary>
public static class BattleEngine
{
    public const int MinDieValue = 1;
    public const int MaxDieValue = 6;

    /// <summary>An attacking country needs this many soldiers to start or continue an attack.</summary>
    public const int MinAttackingSoldiers = 2;

    /// <summary>Minimum soldiers granted for a conquest.</summary>
    public const int MinConquestBonus = 2;

    /// <summary>
    /// Dice per side: the attacker fights with all but one soldier, and the smaller side sets the count.
    /// No cap at 3, unlike classic Risk (the brief's 10 vs 2 example rolls 2 dice).
    /// </summary>
    public static int DiceCount(int attackerSoldiers, int defenderSoldiers)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attackerSoldiers, MinAttackingSoldiers);
        ArgumentOutOfRangeException.ThrowIfLessThan(defenderSoldiers, 1);

        return Math.Min(attackerSoldiers - 1, defenderSoldiers);
    }

    /// <summary>
    /// Pairs dice in the order rolled (die 1 vs die 1, and so on, as the brief specifies; not sorted).
    /// The lower die loses one soldier; a tie costs neither side.
    /// </summary>
    public static BattleRound Resolve(IReadOnlyList<int> attackerDice, IReadOnlyList<int> defenderDice)
    {
        ArgumentNullException.ThrowIfNull(attackerDice);
        ArgumentNullException.ThrowIfNull(defenderDice);
        if (attackerDice.Count == 0 || attackerDice.Count != defenderDice.Count)
        {
            throw new ArgumentException("Both sides must roll the same, non-zero number of dice.");
        }
        EnsureValidDice(attackerDice, nameof(attackerDice));
        EnsureValidDice(defenderDice, nameof(defenderDice));

        int attackerLosses = 0, defenderLosses = 0;
        for (var i = 0; i < attackerDice.Count; i++)
        {
            if (attackerDice[i] > defenderDice[i]) defenderLosses++;
            else if (attackerDice[i] < defenderDice[i]) attackerLosses++;
        }

        return new BattleRound([.. attackerDice], [.. defenderDice], attackerLosses, defenderLosses);
    }

    /// <summary>Rolls one round (attacker's dice first, then defender's) and resolves it.</summary>
    public static BattleRound Fight(int attackerSoldiers, int defenderSoldiers, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(dice);

        var count = DiceCount(attackerSoldiers, defenderSoldiers);
        var attackerDice = dice.Roll(count);
        var defenderDice = dice.Roll(count);
        return Resolve(attackerDice, defenderDice);
    }

    /// <summary>Soldiers gained for a conquest: max(2, floor(soldier value / 2)).</summary>
    public static int ConquestBonus(int conqueredSoldierValue)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(conqueredSoldierValue, SoldierAllocation.MinSoldierValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(conqueredSoldierValue, SoldierAllocation.MaxSoldierValue);

        return Math.Max(MinConquestBonus, conqueredSoldierValue / 2);
    }

    private static void EnsureValidDice(IReadOnlyList<int> dice, string paramName)
    {
        foreach (var value in dice)
        {
            if (value is < MinDieValue or > MaxDieValue)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "Die values must be between 1 and 6.");
            }
        }
    }
}
