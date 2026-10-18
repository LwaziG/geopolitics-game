using Geopolitics.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Geopolitics.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the MongoDB client, database and health check.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // MongoClient is thread-safe and pools connections, so one instance serves the whole app.
        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));

        services.AddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>()
                .GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));

        services.AddHealthChecks()
            .AddCheck<MongoHealthCheck>("mongodb", tags: ["db"]);

        return services;
    }
}
