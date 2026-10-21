using Geopolitics.Domain.Battles;
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

    /// <summary>Set when the game is won; null while in progress or when the human was eliminated.</summary>
    public Guid? WinnerId { get; private set; }

    /// <summary>1-based. A round is complete once every remaining player has taken one turn.</summary>
    public int Round { get; private set; }

    public TurnPhase Phase { get; private set; }

    /// <summary>Soldiers the current player still has to place this turn.</summary>
    public int PendingReinforcements { get; private set; }

    /// <summary>One attack per turn; true once the current player has started theirs.</summary>
    public bool HasAttackedThisTurn { get; private set; }

    /// <summary>The attack in progress (<see cref="TurnPhase.AttackInProgress"/>) or just won (<see cref="TurnPhase.PostConquest"/>).</summary>
    public AttackState? ActiveAttack { get; private set; }

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
        if (Phase != TurnPhase.Reinforce)
        {
            throw new GameRuleException("You have no reinforcements to place.");
        }

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
        EnsureActionPhase("transferring soldiers");

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
        EnsureCanMove(from, count);

        from.RemoveSoldiers(count);
        to.AddSoldiers(count);
        Record(new SoldiersTransferred(Id, playerId, from.Code, to.Code, count));
    }

    /// <summary>Begins this turn's single attack from an owned country on any country the player does not own.</summary>
    public void StartAttack(Guid playerId, string fromCode, string toCode)
    {
        EnsureTurnOf(playerId);
        EnsureActionPhase("attacking");
        if (HasAttackedThisTurn)
        {
            throw new GameRuleException("You have already attacked this turn.");
        }

        var from = GetCountry(fromCode);
        var to = GetCountry(toCode);
        if (!from.IsOwnedBy(playerId))
        {
            throw new GameRuleException("You can only attack from a country you control.");
        }
        if (to.IsOwnedBy(playerId))
        {
            throw new GameRuleException("You cannot attack your own country.");
        }
        if (from.Soldiers < BattleEngine.MinAttackingSoldiers)
        {
            throw new GameRuleException(
                $"{from.Name} needs at least {BattleEngine.MinAttackingSoldiers} soldiers to attack.");
        }

        HasAttackedThisTurn = true;
        ActiveAttack = new AttackState(from.Code, to.Code);
        Phase = TurnPhase.AttackInProgress;
        Record(new AttackStarted(Id, playerId, to.OwnerId, from.Code, to.Code));
    }

    /// <summary>
    /// Rolls one round of the current attack. The attack ends by itself when the defender has no
    /// soldiers left (conquest) or the attacker is down to 1 (failed; the turn continues).
    /// </summary>
    public BattleRound Roll(Guid playerId, IDiceRoller dice)
    {
        ArgumentNullException.ThrowIfNull(dice);
        EnsureTurnOf(playerId);
        var (from, to) = EnsureAttackInProgress();

        var round = BattleEngine.Fight(from.Soldiers, to.Soldiers, dice);
        if (round.AttackerLosses > 0) from.RemoveSoldiers(round.AttackerLosses);
        if (round.DefenderLosses > 0) to.RemoveSoldiers(round.DefenderLosses);
        Record(new DiceRolled(
            Id, playerId, from.Code, to.Code,
            round.AttackerDice, round.DefenderDice,
            round.AttackerLosses, round.DefenderLosses,
            from.Soldiers, to.Soldiers));

        if (to.Soldiers == 0)
        {
            Conquer(playerId, from, to);
        }
        else if (from.Soldiers < BattleEngine.MinAttackingSoldiers)
        {
            Record(new AttackFailed(Id, playerId, from.Code, to.Code));
            ActiveAttack = null;
            Phase = TurnPhase.Action;
        }
        return round;
    }

    /// <summary>Calls off the current attack. Both sides keep their remaining soldiers and the turn ends.</summary>
    public void StopAttack(Guid playerId)
    {
        EnsureTurnOf(playerId);
        var (from, to) = EnsureAttackInProgress();

        Record(new AttackStopped(Id, playerId, from.Code, to.Code));
        ActiveAttack = null;
        Phase = TurnPhase.Action;
        FinishTurn(playerId);
    }

    /// <summary>
    /// After a conquest, moves <paramref name="count"/> extra soldiers (0 is allowed) from the
    /// attacking country into the conquered one, leaving at least 1 behind.
    /// </summary>
    public void MoveAfterConquest(Guid playerId, int count)
    {
        EnsureTurnOf(playerId);
        if (Phase != TurnPhase.PostConquest || ActiveAttack is null)
        {
            throw new GameRuleException("There is no conquered country to move soldiers into.");
        }

        var from = GetCountry(ActiveAttack.FromCode);
        var to = GetCountry(ActiveAttack.ToCode);
        if (count < 0)
        {
            throw new GameRuleException("You cannot move a negative number of soldiers.");
        }
        if (count > 0)
        {
            EnsureCanMove(from, count);
            from.RemoveSoldiers(count);
            to.AddSoldiers(count);
        }

        Record(new SoldiersMovedIn(Id, playerId, from.Code, to.Code, count));
        ActiveAttack = null;
        Phase = TurnPhase.Action;
    }

    /// <summary>Ends the current player's turn and starts the next remaining player's.</summary>
    public void EndTurn(Guid playerId)
    {
        EnsureTurnOf(playerId);
        EnsureActionPhase("ending your turn");
        FinishTurn(playerId);
    }

    /// <summary>Returns the events recorded since the last call and clears them.</summary>
    public IReadOnlyList<GameEvent> DequeueEvents()
    {
        var events = _pendingEvents.ToList();
        _pendingEvents.Clear();
        return events;
    }

    private void Conquer(Guid attackerId, Country from, Country to)
    {
        var defenderId = to.OwnerId;
        var bonus = BattleEngine.ConquestBonus(to.SoldierValue);
        to.Capture(attackerId, bonus);
        Record(new CountryConquered(Id, attackerId, defenderId, from.Code, to.Code, bonus));

        ActiveAttack = new AttackState(from.Code, to.Code);
        Phase = TurnPhase.PostConquest;

        if (CountriesOwnedBy(defenderId).Count == 0)
        {
            var defender = GetPlayer(defenderId);
            defender.Eliminate();
            Record(new PlayerEliminated(Id, defenderId, attackerId));

            if (defender.IsHuman)
            {
                EndGame(GameStatus.Lost, winnerId: null);
                return;
            }
        }

        if (CountriesOwnedBy(attackerId).Count == CountriesPerGame)
        {
            // Only the human can get here: an NPC sweeping the board would have eliminated the human first.
            EndGame(GameStatus.Won, attackerId);
        }
    }

    private void EndGame(GameStatus status, Guid? winnerId)
    {
        Status = status;
        WinnerId = winnerId;
        ActiveAttack = null;
        Phase = TurnPhase.Action;
        Record(new GameEnded(Id, status, winnerId));
    }

    private void FinishTurn(Guid playerId)
    {
        Record(new TurnEnded(Id, playerId));
        AdvanceToNextPlayer();
        StartTurn();
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
        HasAttackedThisTurn = false;
        ActiveAttack = null;
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

    private void EnsureActionPhase(string action)
    {
        var problem = Phase switch
        {
            TurnPhase.Action => null,
            TurnPhase.Reinforce => $"Place your reinforcements before {action}.",
            TurnPhase.AttackInProgress => $"Stop or finish your attack before {action}.",
            TurnPhase.PostConquest => $"Choose how many soldiers to move into the conquered country before {action}.",
            _ => throw new InvalidOperationException($"Unknown phase {Phase}."),
        };
        if (problem is not null)
        {
            throw new GameRuleException(problem);
        }
    }

    private (Country From, Country To) EnsureAttackInProgress()
    {
        if (Phase != TurnPhase.AttackInProgress || ActiveAttack is null)
        {
            throw new GameRuleException("There is no attack in progress.");
        }
        return (GetCountry(ActiveAttack.FromCode), GetCountry(ActiveAttack.ToCode));
    }

    private static void EnsureCanMove(Country from, int count)
    {
        if (count < 1)
        {
            throw new GameRuleException("Move at least one soldier.");
        }
        if (from.Soldiers - count < 1)
        {
            throw new GameRuleException(
                $"{from.Name} must keep at least 1 soldier; you can move up to {from.Soldiers - 1}.");
        }
    }

    private void Record(GameEvent gameEvent) => _pendingEvents.Add(gameEvent);
}
