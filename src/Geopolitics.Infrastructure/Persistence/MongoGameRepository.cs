using Geopolitics.Application.Games;
using Geopolitics.Domain.Games;
using Geopolitics.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>
/// Saves the game snapshot and its new events in one transaction, guarded by the game's version so two
/// overlapping saves cannot both succeed.
/// </summary>
internal sealed class MongoGameRepository(IMongoClient client, IMongoDatabase database, TimeProvider clock)
    : IGameRepository
{
    private readonly IMongoCollection<GameDocument> _games =
        database.GetCollection<GameDocument>(MongoCollections.Games);

    private readonly IMongoCollection<GameEventDocument> _events =
        database.GetCollection<GameEventDocument>(MongoCollections.GameEvents);

    public async Task<Game?> GetAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var document = await _games.Find(g => g.Id == gameId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : Game.Restore(GameDocumentMapper.ToState(document));
    }

    public Task AddAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Version != 0)
        {
            throw new InvalidOperationException("This game has already been stored; use SaveAsync.");
        }
        return WriteAsync(game, isNew: true, cancellationToken);
    }

    public Task SaveAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Version == 0)
        {
            throw new InvalidOperationException("This game has not been stored yet; use AddAsync.");
        }
        return WriteAsync(game, isNew: false, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredGameEvent>> GetEventsAsync(
        Guid gameId, long afterSeq = 0, CancellationToken cancellationToken = default)
    {
        var documents = await _events
            .Find(e => e.GameId == gameId && e.Seq > afterSeq)
            .SortBy(e => e.Seq)
            .ToListAsync(cancellationToken);

        return [.. documents.Select(d => new StoredGameEvent(
            d.Seq,
            GameEventSerializer.Deserialize(d.Type, d.Data),
            new DateTimeOffset(DateTime.SpecifyKind(d.OccurredAt, DateTimeKind.Utc)),
            d.Commentary))];
    }

    private async Task WriteAsync(Game game, bool isNew, CancellationToken cancellationToken)
    {
        var expectedVersion = game.Version;
        var events = game.PendingEvents.ToList();
        var now = clock.GetUtcNow().UtcDateTime;

        var gameDocument = GameDocumentMapper.ToDocument(
            game.ToState() with { Version = expectedVersion + 1, LastEventSeq = game.LastEventSeq + events.Count },
            now);
        var eventDocuments = events.Select((e, i) =>
        {
            var (type, data) = GameEventSerializer.Serialize(e);
            return new GameEventDocument
            {
                GameId = game.Id,
                Seq = game.LastEventSeq + i + 1,
                Type = type,
                Data = data,
                OccurredAt = now,
            };
        }).ToList();

        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            // Retries the whole callback on transient errors (e.g. a write conflict with a concurrent save);
            // the retry then fails the version check and surfaces as a ConcurrencyException.
            await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    if (isNew)
                    {
                        await _games.InsertOneAsync(s, gameDocument, cancellationToken: ct);
                    }
                    else
                    {
                        var result = await _games.ReplaceOneAsync(
                            s, g => g.Id == game.Id && g.Version == expectedVersion, gameDocument, cancellationToken: ct);
                        if (result.MatchedCount == 0)
                        {
                            throw new ConcurrencyException(game.Id, expectedVersion);
                        }
                    }

                    if (eventDocuments.Count > 0)
                    {
                        await _events.InsertManyAsync(s, eventDocuments, cancellationToken: ct);
                    }
                    return true;
                },
                cancellationToken: cancellationToken);
        }
        catch (MongoException ex) when (IsDuplicateKey(ex))
        {
            // Another save got there first: the game id already exists, or these event numbers are taken.
            throw new ConcurrencyException(game.Id, expectedVersion);
        }

        game.MarkSaved(events.Count);
    }

    private static bool IsDuplicateKey(MongoException ex) => ex switch
    {
        MongoWriteException w => w.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException b => b.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey),
        _ => false,
    };
}
