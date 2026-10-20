using Geopolitics.Domain;
using Geopolitics.Domain.Countries;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class CountryDrawTests
{
    private static readonly IReadOnlyList<CountryProfile> Catalogue =
    [
        TestGames.SouthAfrica,
        .. TestGames.Opponents,
        new("FR", "France", 66_000_000),
        new("BR", "Brazil", 212_000_000),
        new("NZ", "New Zealand", 5_200_000),
    ];

    [Fact]
    public void PickOpponents_ReturnsNineDistinctCountriesExcludingThePlayers()
    {
        var picked = CountryDraw.PickOpponents(Catalogue, "za", new Random(3));

        Assert.Equal(9, picked.Count);
        Assert.Equal(9, picked.Select(c => c.Code).Distinct().Count());
        Assert.DoesNotContain(picked, c => c.Code == "ZA");
        Assert.All(picked, c => Assert.Contains(c, Catalogue));
    }

    [Fact]
    public void PickOpponents_VariesBetweenGames()
    {
        var draws = Enumerable.Range(0, 20)
            .Select(seed => string.Join(",", CountryDraw.PickOpponents(Catalogue, "ZA", new Random(seed)).Select(c => c.Code).Order()))
            .Distinct()
            .Count();

        Assert.True(draws > 1);
    }

    [Fact]
    public void PickOpponents_RejectsACountryNotInTheCatalogue()
    {
        Assert.Throws<GameRuleException>(() => CountryDraw.PickOpponents(Catalogue, "XX", new Random()));
    }

    [Fact]
    public void PickOpponents_NeedsAtLeastNineOtherCountries()
    {
        var tooSmall = Catalogue.Take(9).ToList();

        Assert.Throws<InvalidOperationException>(() => CountryDraw.PickOpponents(tooSmall, "ZA", new Random()));
    }

    [Fact]
    public void CountryProfile_NormalisesCodeAndDerivesSoldierValue()
    {
        var profile = new CountryProfile(" za ", " South Africa ", 64_000_000);

        Assert.Equal(("ZA", "South Africa", 5), (profile.Code, profile.Name, profile.SoldierValue));
    }
}
