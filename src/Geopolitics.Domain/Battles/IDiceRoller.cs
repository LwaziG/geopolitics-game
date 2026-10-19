namespace Geopolitics.Domain.Battles;

/// <summary>Source of six-sided dice rolls. Abstracted so tests can script exact outcomes.</summary>
public interface IDiceRoller
{
    /// <summary>Rolls <paramref name="count"/> dice and returns each value (1–6) in the order rolled.</summary>
    IReadOnlyList<int> Roll(int count);
}
