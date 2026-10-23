namespace Geopolitics.Application.Games;

/// <summary>
/// The game was changed by another request (a double-click, a second tab, the NPC loop) between being
/// loaded and saved. Nothing was written; reload the game and try again.
/// </summary>
public sealed class ConcurrencyException(Guid gameId, int expectedVersion)
    : Exception($"Game {gameId} was changed by another request (expected version {expectedVersion}). Reload and try again.")
{
    public Guid GameId { get; } = gameId;

    public int ExpectedVersion { get; } = expectedVersion;
}
