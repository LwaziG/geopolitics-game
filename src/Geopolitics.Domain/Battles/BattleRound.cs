namespace Geopolitics.Domain.Battles;

/// <summary>The dice thrown in one round of an attack and the soldiers each side lost.</summary>
public sealed record BattleRound(
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice,
    int AttackerLosses,
    int DefenderLosses);
