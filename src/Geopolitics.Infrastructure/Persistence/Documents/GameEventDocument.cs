using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Geopolitics.Infrastructure.Persistence.Documents;

/// <summary>One entry in the append-only <c>gameEvents</c> log. (gameId, seq) is unique.</summary>
internal sealed class GameEventDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    public Guid GameId { get; set; }

    public long Seq { get; set; }

    /// <summary>The event's type name, e.g. "DiceRolled".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The event's fields.</summary>
    public BsonDocument Data { get; set; } = [];

    /// <summary>AI commentary, added after the event is stored.</summary>
    public string? Commentary { get; set; }

    public DateTime OccurredAt { get; set; }
}
