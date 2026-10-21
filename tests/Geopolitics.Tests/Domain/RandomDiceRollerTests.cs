using Geopolitics.Domain.Battles;

namespace Geopolitics.Tests.Domain;

public class RandomDiceRollerTests
{
    [Fact]
    public void Roll_ProducesEveryFaceAndNothingElse()
    {
        var roller = new RandomDiceRoller(new Random(42));

        var values = roller.Roll(6_000);

        Assert.All(values, v => Assert.InRange(v, 1, 6));
        Assert.Equal([1, 2, 3, 4, 5, 6], values.Distinct().Order());
    }

    [Fact]
    public void Roll_WithSameSeed_IsReproducible()
    {
        var first = new RandomDiceRoller(new Random(7)).Roll(20);
        var second = new RandomDiceRoller(new Random(7)).Roll(20);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Roll_RejectsZeroDice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RandomDiceRoller().Roll(0));
    }
}
