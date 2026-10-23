using Geopolitics.Domain.Players;

namespace Geopolitics.Domain.Games;

/// <summary>
/// A plain snapshot of a game, used to store and reload it. It carries no behaviour; turn it back into a
/// playable game with <see cref="Game.Restore"/>, which checks every invariant first.
/// </summary>
public sealed record GameState(
    Guid Id,
    string OwnerUserId,
    DateTimeOffset CreatedAt,
    GameStatus Status,
    Guid? WinnerId,
    int Round,
    TurnPhase Phase,
    Guid CurrentPlayerId,
    int PendingReinforcements,
    bool HasAttackedThisTurn,
    AttackState? ActiveAttack,
    IReadOnlyList<Guid> TurnOrder,
    IReadOnlyList<PlayerState> Players,
    IReadOnlyList<CountryState> Countries,
    int Version,
    long LastEventSeq);

public sealed record PlayerState(Guid Id, string Name, PlayerKind Kind, string? UserId, bool IsEliminated);

public sealed record CountryState(
    string Code, string Name, long Population, int SoldierValue, Guid OwnerId, int Soldiers);
