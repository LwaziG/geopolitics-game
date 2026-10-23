using Geopolitics.Domain.Games;
using Geopolitics.Domain.Players;
using Geopolitics.Infrastructure.Persistence.Documents;

namespace Geopolitics.Infrastructure.Persistence;

/// <summary>Converts between the domain's <see cref="GameState"/> and the stored <see cref="GameDocument"/>.</summary>
internal static class GameDocumentMapper
{
    public static GameDocument ToDocument(GameState state, DateTime updatedAtUtc)
    {
        var human = state.Players.Single(p => p.Kind == PlayerKind.Human);
        return new GameDocument
        {
            Id = state.Id,
            OwnerUserId = state.OwnerUserId,
            // Each player is named after their starting country, so this finds the human's original country.
            HumanCountryCode = state.Countries.First(c => c.Name == human.Name).Code,
            Status = state.Status,
            WinnerId = state.WinnerId,
            Round = state.Round,
            Phase = state.Phase,
            CurrentPlayerId = state.CurrentPlayerId,
            PendingReinforcements = state.PendingReinforcements,
            HasAttackedThisTurn = state.HasAttackedThisTurn,
            ActiveAttack = state.ActiveAttack is { } a ? new AttackDocument { FromCode = a.FromCode, ToCode = a.ToCode } : null,
            TurnOrder = [.. state.TurnOrder],
            Players = [.. state.Players.Select(p => new PlayerDocument
            {
                Id = p.Id, Name = p.Name, Kind = p.Kind, UserId = p.UserId, IsEliminated = p.IsEliminated,
            })],
            Countries = [.. state.Countries.Select(c => new GameCountryDocument
            {
                Code = c.Code, Name = c.Name, Population = c.Population,
                SoldierValue = c.SoldierValue, OwnerId = c.OwnerId, Soldiers = c.Soldiers,
            })],
            Version = state.Version,
            LastEventSeq = state.LastEventSeq,
            CreatedAt = state.CreatedAt.UtcDateTime,
            UpdatedAt = updatedAtUtc,
        };
    }

    public static GameState ToState(GameDocument d) => new(
        d.Id,
        d.OwnerUserId,
        new DateTimeOffset(DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc)),
        d.Status,
        d.WinnerId,
        d.Round,
        d.Phase,
        d.CurrentPlayerId,
        d.PendingReinforcements,
        d.HasAttackedThisTurn,
        d.ActiveAttack is { } a ? new AttackState(a.FromCode, a.ToCode) : null,
        [.. d.TurnOrder],
        [.. d.Players.Select(p => new PlayerState(p.Id, p.Name, p.Kind, p.UserId, p.IsEliminated))],
        [.. d.Countries.Select(c => new CountryState(c.Code, c.Name, c.Population, c.SoldierValue, c.OwnerId, c.Soldiers))],
        d.Version,
        d.LastEventSeq);
}
