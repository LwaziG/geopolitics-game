namespace Geopolitics.Domain.Games;

/// <summary>
/// Something that happened in a game. The aggregate records these as it changes; persistence appends
/// them to the event log, which drives the battle log, live spectating and replay.
/// </summary>
public abstract record GameEvent(Guid GameId);

public sealed record GameStarted(Guid GameId, Guid HumanPlayerId, IReadOnlyList<Guid> TurnOrder)
    : GameEvent(GameId);

public sealed record TurnStarted(Guid GameId, Guid PlayerId, int Round, int Reinforcements)
    : GameEvent(GameId);

public sealed record ReinforcementsPlaced(Guid GameId, Guid PlayerId, IReadOnlyDictionary<string, int> Allocation)
    : GameEvent(GameId);

public sealed record SoldiersTransferred(Guid GameId, Guid PlayerId, string FromCode, string ToCode, int Count)
    : GameEvent(GameId);

public sealed record TurnEnded(Guid GameId, Guid PlayerId)
    : GameEvent(GameId);
