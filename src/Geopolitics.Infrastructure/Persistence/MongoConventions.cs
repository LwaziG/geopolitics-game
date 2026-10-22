using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>
/// How documents look in MongoDB: camelCase field names, enums as readable strings, standard UUIDs,
/// and unknown fields ignored so older documents keep loading after a field is removed.
/// </summary>
internal static class MongoConventions
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1) return;

        ConventionRegistry.Register(
            "Geopolitics",
            new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true),
            },
            type => type.Namespace?.StartsWith("Geopolitics", StringComparison.Ordinal) == true);

        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }
}
