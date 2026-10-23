using Geopolitics.Infrastructure.Countries;

namespace Geopolitics.Tests.Infrastructure;

public class CountrySeedFileTests
{
    private static readonly CountrySeedFile Seed = CountrySeedFile.Current;

    [Fact]
    public void ContainsThe195SovereignStates_WithUniqueIsoCodes()
    {
        Assert.Equal(195, Seed.Countries.Count);
        Assert.Equal(195, Seed.Countries.Select(c => c.Code).Distinct().Count());
        Assert.All(Seed.Countries, c => Assert.Matches("^[A-Z]{2}$", c.Code));
        Assert.All(Seed.Countries, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        Assert.All(Seed.Countries, c => Assert.True(c.Population > 0, $"{c.Name} has no population."));
    }

    [Theory]
    [InlineData("VA", "Vatican City", 1)]     // UN observer state; the brief's 1-soldier example
    [InlineData("PS", "Palestine", 2)]        // UN observer state
    [InlineData("IN", "India", 9)]            // the brief's 9-soldier example
    [InlineData("ZA", "South Africa", 5)]
    [InlineData("CI", "Côte d'Ivoire", 4)]
    public void IncludesExpectedCountries(string code, string name, int soldierValue)
    {
        var profile = Assert.Single(Seed.ToProfiles(), c => c.Code == code);

        Assert.Equal((name, soldierValue), (profile.Name, profile.SoldierValue));
    }

    [Theory]
    [InlineData("TW")]   // not a UN member or observer state
    [InlineData("HK")]
    [InlineData("PR")]
    [InlineData("GL")]
    [InlineData("EH")]
    public void ExcludesDependenciesAndTerritories(string code)
    {
        Assert.DoesNotContain(Seed.Countries, c => c.Code == code);
    }

    [Fact]
    public void RecordsWhereAndWhenTheDataWasCaptured()
    {
        Assert.Equal("https://www.worldometers.info/world-population/population-by-country/", Seed.Source);
        Assert.Equal(new DateOnly(2026, 10, 23), Seed.CapturedOn);
        Assert.Equal(2026, Seed.PopulationYear);
    }
}
