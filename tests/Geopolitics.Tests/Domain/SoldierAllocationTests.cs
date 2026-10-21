using Geopolitics.Domain.Countries;

namespace Geopolitics.Tests.Domain;

public class SoldierAllocationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(800, 1)]                  // Vatican City
    [InlineData(5_000_000, 1)]            // band limits are inclusive
    [InlineData(5_000_001, 2)]
    [InlineData(10_000_000, 2)]
    [InlineData(10_000_001, 3)]
    [InlineData(25_000_000, 3)]
    [InlineData(25_000_001, 4)]
    [InlineData(50_000_000, 4)]
    [InlineData(50_000_001, 5)]
    [InlineData(75_000_000, 5)]
    [InlineData(75_000_001, 6)]
    [InlineData(100_000_000, 6)]
    [InlineData(100_000_001, 7)]
    [InlineData(125_000_000, 7)]
    [InlineData(125_000_001, 8)]
    [InlineData(150_000_000, 8)]
    [InlineData(150_000_001, 9)]
    [InlineData(1_460_000_000, 9)]        // India
    public void FromPopulation_UsesBriefBands(long population, int expectedSoldiers)
    {
        Assert.Equal(expectedSoldiers, SoldierAllocation.FromPopulation(population));
    }

    [Fact]
    public void FromPopulation_RejectsNegativePopulation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SoldierAllocation.FromPopulation(-1));
    }
}
