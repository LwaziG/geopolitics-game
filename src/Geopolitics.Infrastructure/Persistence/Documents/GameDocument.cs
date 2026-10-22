using Geopolitics.Domain.Games;
using Geopolitics.Domain.Players;
using MongoDB.Bson.Serialization.Attributes;

namespace Geopolitics.Infrastructure.Persistence.Documents;

/// <summary>A game in the <c>games</c> collection. Players, countries and the attack are embedded.</summary>
internal sealed class GameDocument
{
    [BsonId]
    public Guid Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>The human's starting country, kept for searching and listing games.</summary>
    public string HumanCountryCode { get; set; } = string.Empty;

    public GameStatus Status { get; set; }

    public Guid? WinnerId { get; set; }

    public int Round { get; set; }

    public TurnPhase Phase { get; set; }

    public Guid CurrentPlayerId { get; set; }

    public int PendingReinforcements { get; set; }

    public bool HasAttackedThisTurn { get; set; }

    public AttackDocument? ActiveAttack { get; set; }

    public List<Guid> TurnOrder { get; set; } = [];

    public List<PlayerDocument> Players { get; set; } = [];

    public List<GameCountryDocument> Countries { get; set; } = [];

    /// <summary>Incremented on every save; a save only succeeds if the stored value still matches.</summary>
    public int Version { get; set; }

    public long LastEventSeq { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

internal sealed class PlayerDocument
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public PlayerKind Kind { get; set; }

    public string? UserId { get; set; }

    public bool IsEliminated { get; set; }
}

internal sealed class GameCountryDocument
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long Population { get; set; }

    public int SoldierValue { get; set; }

    public Guid OwnerId { get; set; }

    public int Soldiers { get; set; }
}

internal sealed class AttackDocument
{
    public string FromCode { get; set; } = string.Empty;

    public string ToCode { get; set; } = string.Empty;
}
