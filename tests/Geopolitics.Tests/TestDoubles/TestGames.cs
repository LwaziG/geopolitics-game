using Geopolitics.Domain.Countries;
using Geopolitics.Domain.Games;

namespace Geopolitics.Tests.TestDoubles;

/// <summary>Builds games from a fixed set of ten countries for tests.</summary>
public static class TestGames
{
    public const string OwnerUserId = "user-1";

    public static readonly CountryProfile SouthAfrica = new("ZA", "South Africa", 64_000_000);   // 5 soldiers

    public static readonly IReadOnlyList<CountryProfile> Opponents =
    [
        new("IN", "India", 1_460_000_000),        // 9
        new("VA", "Vatican City", 800),           // 1
        new("NG", "Nigeria", 230_000_000),        // 9
        new("KE", "Kenya", 56_000_000),           // 5
        new("BW", "Botswana", 2_500_000),         // 1
        new("DE", "Germany", 84_000_000),         // 6
        new("PT", "Portugal", 10_400_000),        // 3
        new("JP", "Japan", 123_000_000),          // 7
        new("PE", "Peru", 34_000_000),            // 4
    ];

    public static Game Create(int seed = 1) =>
        Game.Create(Guid.NewGuid(), OwnerUserId, SouthAfrica, Opponents, new Random(seed));

    /// <summary>Ends every player's turn in round 1, leaving the first player of round 2 to move.</summary>
    public static Game CreateInRoundTwo(int seed = 1)
    {
        var game = Create(seed);
        for (var i = 0; i < Game.CountriesPerGame; i++)
        {
            game.EndTurn(game.CurrentPlayer.Id);
        }
        return game;
    }
}
