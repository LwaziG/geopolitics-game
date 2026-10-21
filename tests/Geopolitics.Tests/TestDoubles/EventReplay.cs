using Geopolitics.Domain.Games;

namespace Geopolitics.Tests.TestDoubles;

/// <summary>
/// Rebuilds the board from the event log alone. If it ever disagrees with the live game,
/// some state change was not recorded, and replay and spectating would show the wrong board.
/// </summary>
public sealed class EventReplay
{
    private readonly Dictionary<string, (Guid Owner, int Soldiers)> _board = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _eliminated = [];

    public GameStatus Status { get; private set; } = GameStatus.InProgress;

    public void Apply(IEnumerable<GameEvent> events)
    {
        foreach (var e in events)
        {
            Apply(e);
        }
    }

    public void AssertMatches(Game game)
    {
        foreach (var country in game.Countries)
        {
            Assert.True(_board.TryGetValue(country.Code, out var replayed), $"{country.Code} missing from replay.");
            Assert.Equal((country.OwnerId, country.Soldiers), replayed);
        }
        Assert.Equal(game.Countries.Count, _board.Count);
        Assert.Equal(game.Players.Where(p => p.IsEliminated).Select(p => p.Id).Order(), _eliminated.Order());
        Assert.Equal(game.Status, Status);
    }

    private void Apply(GameEvent e)
    {
        switch (e)
        {
            case GameStarted started:
                foreach (var c in started.Countries) _board[c.Code] = (c.OwnerId, c.Soldiers);
                break;
            case ReinforcementsPlaced placed:
                foreach (var (code, count) in placed.Allocation) Add(code, count);
                break;
            case SoldiersTransferred transferred:
                Add(transferred.FromCode, -transferred.Count);
                Add(transferred.ToCode, transferred.Count);
                break;
            case DiceRolled rolled:
                _board[rolled.FromCode] = (_board[rolled.FromCode].Owner, rolled.AttackerSoldiersLeft);
                _board[rolled.ToCode] = (_board[rolled.ToCode].Owner, rolled.DefenderSoldiersLeft);
                break;
            case CountryConquered conquered:
                _board[conquered.ToCode] = (conquered.AttackerId, conquered.BonusSoldiers);
                break;
            case SoldiersMovedIn moved:
                Add(moved.FromCode, -moved.Count);
                Add(moved.ToCode, moved.Count);
                break;
            case PlayerEliminated eliminated:
                _eliminated.Add(eliminated.PlayerId);
                break;
            case GameEnded ended:
                Status = ended.Status;
                break;
        }
    }

    private void Add(string code, int delta) => _board[code] = (_board[code].Owner, _board[code].Soldiers + delta);
}
