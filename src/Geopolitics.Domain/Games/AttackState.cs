namespace Geopolitics.Domain.Games;

/// <summary>The attack in progress, or the conquest awaiting a soldier move.</summary>
public sealed record AttackState(string FromCode, string ToCode);
