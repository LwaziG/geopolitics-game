using System.Text.Json;
using System.Text.Json.Serialization;
using Geopolitics.Domain.Games;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>
/// Converts domain events to and from the <c>data</c> field of a stored event. System.Text.Json handles the
/// events' positional records natively and keeps the stored fields readable (camelCase, enums as names).
/// </summary>
internal static class GameEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonWriterSettings BsonToJson = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

    private static readonly Dictionary<string, Type> EventTypes = typeof(GameEvent).Assembly
        .GetTypes()
        .Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract)
        .ToDictionary(t => t.Name, StringComparer.Ordinal);

    public static (string Type, BsonDocument Data) Serialize(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        var type = gameEvent.GetType();
        return (type.Name, BsonDocument.Parse(JsonSerializer.Serialize(gameEvent, type, Options)));
    }

    public static GameEvent Deserialize(string type, BsonDocument data)
    {
        if (!EventTypes.TryGetValue(type, out var eventType))
        {
            throw new InvalidOperationException($"Unknown game event type '{type}'.");
        }
        return (GameEvent)(JsonSerializer.Deserialize(data.ToJson(BsonToJson), eventType, Options)
            ?? throw new InvalidOperationException($"Stored '{type}' event has no data."));
    }
}
