namespace Geopolitics.Domain.Players;

public enum PlayerKind
{
    Human,
    Npc,
}

/// <summary>One of the ten players in a game: the logged-in user or an NPC.</summary>
public sealed class Player
{
    internal Player(Guid id, string name, PlayerKind kind, string? userId, bool isEliminated = false)
    {
        Id = id;
        Name = name;
        Kind = kind;
        UserId = userId;
        IsEliminated = isEliminated;
    }

    public Guid Id { get; }

    /// <summary>Display name; the name of the player's starting country.</summary>
    public string Name { get; }

    public PlayerKind Kind { get; }

    /// <summary>The account that controls this player; null for NPCs.</summary>
    public string? UserId { get; }

    public bool IsHuman => Kind == PlayerKind.Human;

    public bool IsEliminated { get; private set; }

    internal void Eliminate() => IsEliminated = true;
}
