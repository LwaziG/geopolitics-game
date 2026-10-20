using Geopolitics.Domain.Countries;
using Geopolitics.Domain.Players;

namespace Geopolitics.Domain.Games;

/// <summary>
/// One single-player game: the aggregate root that owns the board, the players and the turn.
/// Every move is a method here that checks the rules first and only then changes state,
/// recording a <see cref="GameEvent"/> for each change.
/// </summary>
public sealed class Game
{
    public const int CountriesPerGame = CountryDraw.OpponentCount + 1;

    private readonly List<Country> _countries;
    private readonly List<Player> _players;
    private readonly List<Guid> _turnOrder;
    private readonly List<GameEvent> _pendingEvents = [];
    private int _turnIndex;

    private Game(Guid id, string ownerUserId, List<Player> players, List<Country> countries, List<Guid> turnOrder)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        _players = players;
        _countries = countries;
        _turnOrder = turnOrder;
        Status = GameStatus.InProgress;
        Round = 1;
    }

    public Guid Id { get; }

    /// <summary>The account that started the game and is the only one allowed to play the human's turns.</summary>
    public string OwnerUserId { get; }

    public GameStatus Status { get; private set; }

    /// <summary>1-based. A round is complete once every remaining player has taken one turn.</summary>
    public int Round { get; private set; }

    public TurnPhase Phase { get; private set; }

    /// <summary>Soldiers the current player still has to place this turn.</summary>
    public int PendingReinforcements { get; private set; }

    public IReadOnlyList<Country> Countries => _countries;

    public IReadOnlyList<Player> Players => _players;

    public IReadOnlyList<Guid> TurnOrder => _turnOrder;

    public Player CurrentPlayer => GetPlayer(_turnOrder[_turnIndex]);

    public Player HumanPlayer => _players.Single(p => p.IsHuman);

    /// <summary>Events recorded since the last <see cref="DequeueEvents"/>.</summary>
    public IReadOnlyList<GameEvent> PendingEvents => _pendingEvents;

    /// <summary>
    /// Starts a game: the human holds <paramref name="humanCountry"/>, each of the nine
    /// <paramref name="npcCountries"/> is held by an NPC, and the turn order is shuffled.
    /// </summary>
    public static Game Create(
        Guid id,
        string ownerUserId,
        CountryProfile humanCountry,
        IReadOnlyList<CountryProfile> npcCountries,
        Random random)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(humanCountry);
        ArgumentNullException.ThrowIfNull(npcCountries);
        ArgumentNullException.ThrowIfNull(random);

        if (npcCountries.Count != CountryDraw.OpponentCount)
        {
            throw new ArgumentException($"A game needs exactly {CountryDraw.OpponentCount} NPC countries.", nameof(npcCountries));
        }
        var allProfiles = npcCountries.Prepend(humanCountry).ToList();
        if (allProfiles.DistinctBy(p => p.Code).Count() != CountriesPerGame)
        {
            throw new ArgumentException("All ten countries in a game must be different.", nameof(npcCountries));
        }

        var players = new List<Player>(CountriesPerGame);
        var countries = new List<Country>(CountriesPerGame);
        foreach (var profile in allProfiles)
        {
            var isHuman = ReferenceEquals(profile, humanCountry);
            var player = new Player(
                Guid.NewGuid(),
                profile.Name,
                isHuman ? PlayerKind.Human : PlayerKind.Npc,
                isHuman ? ownerUserId : null);
            players.Add(player);
            countries.Add(new Country(profile, player.Id));
        }

        var turnOrder = players.Select(p => p.Id).ToArray();
        random.Shuffle(turnOrder);

        var game = new Game(id, ownerUserId, players, countries, [.. turnOrder]);
        game.Record(new GameStarted(id, game.HumanPlayer.Id, game.TurnOrder));
        game.StartTurn();
        return game;
    }

    public Player GetPlayer(Guid playerId) =>
        _players.SingleOrDefault(p => p.Id == playerId)
        ?? throw new GameRuleException("That player is not in this game.");

    public Country GetCountry(string code) =>
        _countries.SingleOrDefault(c => string.Equals(c.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new GameRuleException($"'{code}' is not a country in this game.");

    public IReadOnlyList<Country> CountriesOwnedBy(Guid playerId) =>
        [.. _countries.Where(c => c.IsOwnedBy(playerId))];

    /// <summary>Places all of this turn's reinforcements in one go, in any split across owned countries.</summary>
    public void PlaceReinforcements(Guid playerId, IReadOnlyDictionary<string, int> allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        EnsureTurnOf(playerId);
        EnsurePhase(TurnPhase.Reinforce, "You have no reinforcements to place.");

        if (allocation.Count == 0 || allocation.Values.Any(n => n < 1))
        {
            throw new GameRuleException("Place at least one soldier on each country you choose.");
        }
        var placements = allocation.Select(kv => (Country: GetCountry(kv.Key), Count: kv.Value)).ToList();
        if (placements.Any(p => !p.Country.IsOwnedBy(playerId)))
        {
            throw new GameRuleException("You can only reinforce countries you control.");
        }
        var total = placements.Sum(p => p.Count);
        if (total != PendingReinforcements)
        {
            throw new GameRuleException(
                $"You must place exactly {PendingReinforcements} soldiers; you placed {total}.");
        }

        foreach (var (country, count) in placements)
        {
            country.AddSoldiers(count);
        }
        PendingReinforcements = 0;
        Phase = TurnPhase.Action;
        Record(new ReinforcementsPlaced(
            Id, playerId, placements.ToDictionary(p => p.Country.Code, p => p.Count)));
    }

    /// <summary>Moves soldiers between two countries the player controls, leaving at least one behind.</summary>
    public void Transfer(Guid playerId, string fromCode, string toCode, int count)
    {
        EnsureTurnOf(playerId);
        EnsurePhase(TurnPhase.Action, Phase switch
        {
            TurnPhase.Reinforce => "Place your reinforcements before transferring soldiers.",
            TurnPhase.AttackInProgress => "You cannot transfer soldiers while an attack is in progress.",
            _ => "Finish moving soldiers into the conquered country first.",
        });

        var from = GetCountry(fromCode);
        var to = GetCountry(toCode);
        if (from == to)
        {
            throw new GameRuleException("Choose two different countries.");
        }
        if (!from.IsOwnedBy(playerId) || !to.IsOwnedBy(playerId))
        {
            throw new GameRuleException("You can only transfer between countries you control.");
        }
        if (count < 1)
        {
            throw new GameRuleException("Transfer at least one soldier.");
        }
        if (from.Soldiers - count < 1)
        {
            throw new GameRuleException($"{from.Name} must keep at least 1 soldier; you can move up to {from.Soldiers - 1}.");
        }

        from.RemoveSoldiers(count);
        to.AddSoldiers(count);
        Record(new SoldiersTransferred(Id, playerId, from.Code, to.Code, count));
    }

    /// <summary>Ends the current player's turn and starts the next remaining player's.</summary>
    public void EndTurn(Guid playerId)
    {
        EnsureTurnOf(playerId);
        EnsurePhase(TurnPhase.Action, Phase switch
        {
            TurnPhase.Reinforce => "Place your reinforcements before ending your turn.",
            TurnPhase.AttackInProgress => "Stop or finish your attack before ending your turn.",
            _ => "Finish moving soldiers into the conquered country first.",
        });

        Record(new TurnEnded(Id, playerId));
        AdvanceToNextPlayer();
        StartTurn();
    }

    /// <summary>Returns the events recorded since the last call and clears them.</summary>
    public IReadOnlyList<GameEvent> DequeueEvents()
    {
        var events = _pendingEvents.ToList();
        _pendingEvents.Clear();
        return events;
    }

    private void AdvanceToNextPlayer()
    {
        var index = _turnIndex;
        for (var step = 0; step < _turnOrder.Count; step++)
        {
            index = (index + 1) % _turnOrder.Count;
            if (index == 0)
            {
                // Passed the start of the turn order: everyone remaining has had a turn.
                Round++;
            }
            if (!GetPlayer(_turnOrder[index]).IsEliminated)
            {
                _turnIndex = index;
                return;
            }
        }
        throw new InvalidOperationException("No players remain.");
    }

    private void StartTurn()
    {
        var player = CurrentPlayer;
        if (Round >= Reinforcements.FirstRound)
        {
            PendingReinforcements = Reinforcements.For(CountriesOwnedBy(player.Id).Count);
            Phase = TurnPhase.Reinforce;
        }
        else
        {
            PendingReinforcements = 0;
            Phase = TurnPhase.Action;
        }
        Record(new TurnStarted(Id, player.Id, Round, PendingReinforcements));
    }

    private void EnsureTurnOf(Guid playerId)
    {
        if (Status != GameStatus.InProgress)
        {
            throw new GameRuleException("This game is over.");
        }
        if (CurrentPlayer.Id != playerId)
        {
            throw new GameRuleException("It is not your turn.");
        }
    }

    private void EnsurePhase(TurnPhase required, string message)
    {
        if (Phase != required)
        {
            throw new GameRuleException(message);
        }
    }

    private void Record(GameEvent gameEvent) => _pendingEvents.Add(gameEvent);
}
