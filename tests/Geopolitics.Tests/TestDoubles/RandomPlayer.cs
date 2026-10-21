using Geopolitics.Domain.Battles;
using Geopolitics.Domain.Games;

namespace Geopolitics.Tests.TestDoubles;

/// <summary>
/// Plays a whole turn of random but legal moves for whoever's turn it is, calling
/// <c>afterMove</c> after every move so the caller can check invariants.
/// </summary>
public sealed class RandomPlayer(Random random, IDiceRoller dice)
{
    private const double AttackChance = 0.85;
    private const double StopChance = 0.05;

    public void PlayTurn(Game game, Action afterMove)
    {
        var player = game.CurrentPlayer.Id;

        if (game.Phase == TurnPhase.Reinforce)
        {
            game.PlaceReinforcements(player, RandomSplit(game, player, game.PendingReinforcements));
            afterMove();
        }

        TransferRandomly(game, player, afterMove);

        var attackers = game.CountriesOwnedBy(player).Where(c => c.Soldiers >= BattleEngine.MinAttackingSoldiers).ToList();
        if (attackers.Count > 0 && random.NextDouble() < AttackChance)
        {
            var from = Pick(attackers);
            var to = Pick(game.Countries.Where(c => !c.IsOwnedBy(player)).ToList());
            game.StartAttack(player, from.Code, to.Code);
            afterMove();

            while (game.Phase == TurnPhase.AttackInProgress)
            {
                if (random.NextDouble() < StopChance)
                {
                    game.StopAttack(player);   // ends the turn
                    afterMove();
                    return;
                }
                game.Roll(player, dice);
                afterMove();
            }

            if (game.Status != GameStatus.InProgress) return;

            if (game.Phase == TurnPhase.PostConquest)
            {
                game.MoveAfterConquest(player, random.Next(0, from.Soldiers));
                afterMove();
            }
        }

        TransferRandomly(game, player, afterMove);
        game.EndTurn(player);
        afterMove();
    }

    private void TransferRandomly(Game game, Guid player, Action afterMove)
    {
        var transfers = random.Next(0, 3);
        for (var i = 0; i < transfers; i++)
        {
            var owned = game.CountriesOwnedBy(player);
            var sources = owned.Where(c => c.Soldiers > 1).ToList();
            if (owned.Count < 2 || sources.Count == 0) return;

            var from = Pick(sources);
            var to = Pick(owned.Where(c => c != from).ToList());
            game.Transfer(player, from.Code, to.Code, random.Next(1, from.Soldiers));
            afterMove();
        }
    }

    private Dictionary<string, int> RandomSplit(Game game, Guid player, int soldiers)
    {
        var owned = game.CountriesOwnedBy(player);
        var split = new Dictionary<string, int>();
        for (var i = 0; i < soldiers; i++)
        {
            var code = Pick(owned).Code;
            split[code] = split.GetValueOrDefault(code) + 1;
        }
        return split;
    }

    private T Pick<T>(IReadOnlyList<T> items) => items[random.Next(items.Count)];
}
