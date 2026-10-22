using Geopolitics.Domain.Battles;
using Geopolitics.Domain.Countries;
using Geopolitics.Domain.Players;

namespace Geopolitics.Domain.Games;

/// <summary>
/// Checks that a stored snapshot describes a position the rules could have produced, so a corrupted or
/// hand-edited document is rejected on load instead of breaking the game later.
/// </summary>
internal static class GameStateValidator
{
    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Require(state.Id != Guid.Empty, "game id is empty");
        Require(!string.IsNullOrWhiteSpace(state.OwnerUserId), "owner is missing");
        Require(state.Round >= 1, "round must be at least 1");
        Require(state.Version >= 0 && state.LastEventSeq >= 0, "version and event sequence cannot be negative");

        ValidatePlayers(state);
        ValidateCountries(state);
        ValidateTurn(state);
        ValidateOutcome(state);
    }

    private static void ValidatePlayers(GameState state)
    {
        var players = state.Players;
        Require(players.Count == Game.CountriesPerGame, $"expected {Game.CountriesPerGame} players, found {players.Count}");
        Require(players.Select(p => p.Id).Distinct().Count() == players.Count, "player ids are not unique");

        var humans = players.Where(p => p.Kind == PlayerKind.Human).ToList();
        Require(humans.Count == 1, "there must be exactly one human player");
        Require(humans[0].UserId == state.OwnerUserId, "the human player is not linked to the game's owner");
        Require(players.All(p => p.Kind == PlayerKind.Human || p.UserId is null), "NPCs cannot be linked to a user");

        Require(state.TurnOrder.Count == players.Count
                && state.TurnOrder.Order().SequenceEqual(players.Select(p => p.Id).Order()),
            "turn order must list every player exactly once");
    }

    private static void ValidateCountries(GameState state)
    {
        var countries = state.Countries;
        var playerIds = state.Players.Select(p => p.Id).ToHashSet();

        Require(countries.Count == Game.CountriesPerGame, $"expected {Game.CountriesPerGame} countries, found {countries.Count}");
        Require(countries.Select(c => c.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() == countries.Count,
            "country codes are not unique");

        foreach (var c in countries)
        {
            Require(!string.IsNullOrWhiteSpace(c.Code) && !string.IsNullOrWhiteSpace(c.Name), "a country is missing its code or name");
            Require(c.Population >= 0 && c.SoldierValue == SoldierAllocation.FromPopulation(c.Population),
                $"{c.Code} has soldier value {c.SoldierValue}, which does not match its population");
            Require(c.Soldiers >= 1, $"{c.Code} has {c.Soldiers} soldiers; every country must have at least 1");
            Require(playerIds.Contains(c.OwnerId), $"{c.Code} is owned by a player who is not in the game");
        }

        foreach (var p in state.Players)
        {
            var owns = countries.Any(c => c.OwnerId == p.Id);
            Require(p.IsEliminated != owns,
                p.IsEliminated ? $"{p.Name} is eliminated but still owns a country" : $"{p.Name} owns no country but is not eliminated");
        }
    }

    private static void ValidateTurn(GameState state)
    {
        Require(state.TurnOrder.Contains(state.CurrentPlayerId), "the current player is not in the turn order");
        if (state.Status != GameStatus.InProgress)
        {
            Require(state.ActiveAttack is null, "a finished game cannot have an attack in progress");
            return;
        }

        var current = state.Players.Single(p => p.Id == state.CurrentPlayerId);
        Require(!current.IsEliminated, "the current player has been eliminated");
        Require((state.Phase == TurnPhase.Reinforce) == (state.PendingReinforcements > 0),
            "pending reinforcements must be positive exactly when the phase is Reinforce");
        Require(state.PendingReinforcements >= 0, "pending reinforcements cannot be negative");

        var attackPhase = state.Phase is TurnPhase.AttackInProgress or TurnPhase.PostConquest;
        Require(attackPhase == state.ActiveAttack is not null, $"phase {state.Phase} does not match the attack state");
        if (state.ActiveAttack is not { } attack) return;

        Require(state.HasAttackedThisTurn, "an attack is recorded but the player has not attacked this turn");
        var from = state.Countries.SingleOrDefault(c => c.Code == attack.FromCode);
        var to = state.Countries.SingleOrDefault(c => c.Code == attack.ToCode);
        Require(from is not null && to is not null, "the attack refers to a country that is not in the game");
        Require(from!.OwnerId == current.Id, "the attack's origin is not owned by the current player");
        if (state.Phase == TurnPhase.AttackInProgress)
        {
            Require(to!.OwnerId != current.Id, "the current player cannot be attacking their own country");
            Require(from.Soldiers >= BattleEngine.MinAttackingSoldiers, "the attacking country has too few soldiers to continue");
        }
        else
        {
            Require(to!.OwnerId == current.Id, "after a conquest the target must belong to the current player");
        }
    }

    private static void ValidateOutcome(GameState state)
    {
        var human = state.Players.Single(p => p.Kind == PlayerKind.Human);
        switch (state.Status)
        {
            case GameStatus.InProgress:
                Require(state.WinnerId is null, "a game in progress cannot have a winner");
                Require(!human.IsEliminated, "the human is eliminated but the game is still in progress");
                break;
            case GameStatus.Won:
                Require(state.WinnerId == human.Id && state.Countries.All(c => c.OwnerId == human.Id),
                    "a won game must have the human as winner, owning every country");
                break;
            case GameStatus.Lost:
                Require(state.WinnerId is null && human.IsEliminated, "a lost game must have the human eliminated and no winner");
                break;
            default:
                Require(false, $"unknown status {state.Status}");
                break;
        }
    }

    private const string StateParameter = "state";

    private static void Require(bool condition, string problem)
    {
        if (!condition)
        {
            throw new ArgumentException($"Invalid game state: {problem}.", StateParameter);
        }
    }
}
