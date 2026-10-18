using System.ComponentModel.DataAnnotations;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>Settings bound from the "Mongo" configuration section.</summary>
public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    public string Database { get; init; } = string.Empty;
}
