using Geopolitics.Domain.Games;

namespace Geopolitics.Application.Games;

/// <summary>Stores games as a snapshot plus an append-only event log.</summary>
public interface IGameRepository
{
    /// <summary>Loads a game, or null if there is none with that id.</summary>
    Task<Game?> GetAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Stores a new game (version 0) and its pending events.</summary>
    /// <exception cref="ConcurrencyException">A game with this id already exists.</exception>
    Task AddAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the game's changes and pending events atomically, provided nobody else has saved it since it was
    /// loaded. On success the game's version is bumped and the saved events are cleared from it.
    /// </summary>
    /// <exception cref="ConcurrencyException">The stored game has changed since this copy was loaded.</exception>
    Task SaveAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>Events with a sequence number greater than <paramref name="afterSeq"/>, in order.</summary>
    Task<IReadOnlyList<StoredGameEvent>> GetEventsAsync(
        Guid gameId, long afterSeq = 0, CancellationToken cancellationToken = default);
}

/// <summary>An event as stored in the log, with its position and (later) its AI commentary.</summary>
public sealed record StoredGameEvent(long Seq, GameEvent Event, DateTimeOffset OccurredAt, string? Commentary);
