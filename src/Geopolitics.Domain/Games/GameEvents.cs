namespace Geopolitics.Domain.Games;

/// <summary>
/// Something that happened in a game. The aggregate records these as it changes; persistence appends
/// them to the event log, which drives the battle log, live spectating and replay.
/// </summary>
public abstract record GameEvent(Guid GameId);

/// <summary>The starting board, so the event log alone is enough to replay a game.</summary>
public sealed record GameStarted(
    Guid GameId,
    Guid HumanPlayerId,
    IReadOnlyList<Guid> TurnOrder,
    IReadOnlyList<StartingCountry> Countries)
    : GameEvent(GameId);

public sealed record StartingCountry(string Code, string Name, int SoldierValue, Guid OwnerId, int Soldiers);

public sealed record TurnStarted(Guid GameId, Guid PlayerId, int Round, int Reinforcements)
    : GameEvent(GameId);

public sealed record ReinforcementsPlaced(Guid GameId, Guid PlayerId, IReadOnlyDictionary<string, int> Allocation)
    : GameEvent(GameId);

public sealed record SoldiersTransferred(Guid GameId, Guid PlayerId, string FromCode, string ToCode, int Count)
    : GameEvent(GameId);

public sealed record AttackStarted(Guid GameId, Guid AttackerId, Guid DefenderId, string FromCode, string ToCode)
    : GameEvent(GameId);

/// <summary>One round of dice, with the actual values so replays are exact.</summary>
public sealed record DiceRolled(
    Guid GameId,
    Guid AttackerId,
    string FromCode,
    string ToCode,
    IReadOnlyList<int> AttackerDice,
    IReadOnlyList<int> DefenderDice,
    int AttackerLosses,
    int DefenderLosses,
    int AttackerSoldiersLeft,
    int DefenderSoldiersLeft)
    : GameEvent(GameId);

/// <summary>The attacker was reduced to 1 soldier; the defender keeps the country.</summary>
public sealed record AttackFailed(Guid GameId, Guid AttackerId, string FromCode, string ToCode)
    : GameEvent(GameId);

/// <summary>The attacker chose to stop; their turn ends.</summary>
public sealed record AttackStopped(Guid GameId, Guid AttackerId, string FromCode, string ToCode)
    : GameEvent(GameId);

public sealed record CountryConquered(
    Guid GameId, Guid AttackerId, Guid DefenderId, string FromCode, string ToCode, int BonusSoldiers)
    : GameEvent(GameId);

public sealed record SoldiersMovedIn(Guid GameId, Guid PlayerId, string FromCode, string ToCode, int Count)
    : GameEvent(GameId);

public sealed record PlayerEliminated(Guid GameId, Guid PlayerId, Guid EliminatedById)
    : GameEvent(GameId);

/// <summary>The game is over. <paramref name="WinnerId"/> is null when it ended because the human was eliminated.</summary>
public sealed record GameEnded(Guid GameId, GameStatus Status, Guid? WinnerId)
    : GameEvent(GameId);

public sealed record TurnEnded(Guid GameId, Guid PlayerId)
    : GameEvent(GameId);
