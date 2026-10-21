using Geopolitics.Domain.Battles;
using Geopolitics.Tests.TestDoubles;

namespace Geopolitics.Tests.Domain;

public class BattleEngineTests
{
    // Brief p. 3: "10 vs 2 -> 2 dice", "3 vs 5 -> 2 dice", "2 vs 5 -> 1 die".
    [Theory]
    [InlineData(10, 2, 2)]
    [InlineData(3, 5, 2)]
    [InlineData(2, 5, 1)]
    [InlineData(4, 1, 1)]
    [InlineData(9, 9, 8)]   // no cap at 3
    public void DiceCount_IsSmallerOfAttackersMinusOneAndDefenders(int attacker, int defender, int expected)
    {
        Assert.Equal(expected, BattleEngine.DiceCount(attacker, defender));
    }

    [Theory]
    [InlineData(1, 3)]   // attacker needs at least 2 soldiers
    [InlineData(5, 0)]   // defender must have soldiers
    public void DiceCount_RejectsImpossibleBattles(int attacker, int defender)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEngine.DiceCount(attacker, defender));
    }

    [Fact]
    public void Resolve_BriefExample_EachSideLosesOne()
    {
        // Brief p. 3: attacker [1, 4] vs defender [2, 3].
        var round = BattleEngine.Resolve([1, 4], [2, 3]);

        Assert.Equal(1, round.AttackerLosses);
        Assert.Equal(1, round.DefenderLosses);
    }

    [Fact]
    public void Resolve_PairsDiceInRollOrder_NotSorted()
    {
        // In roll order: 6 beats 1, 1 loses to 6. Sorted (classic Risk) would be two ties.
        var round = BattleEngine.Resolve([6, 1], [1, 6]);

        Assert.Equal(1, round.AttackerLosses);
        Assert.Equal(1, round.DefenderLosses);
    }

    [Fact]
    public void Resolve_TiesCostNoSoldiers()
    {
        var round = BattleEngine.Resolve([3, 5], [3, 5]);

        Assert.Equal(0, round.AttackerLosses);
        Assert.Equal(0, round.DefenderLosses);
    }

    [Fact]
    public void Resolve_HigherDiceWinEveryPair()
    {
        var round = BattleEngine.Resolve([6, 6, 2], [1, 2, 5]);

        Assert.Equal(1, round.AttackerLosses);
        Assert.Equal(2, round.DefenderLosses);
    }

    [Fact]
    public void Resolve_KeepsDiceForDisplay()
    {
        var round = BattleEngine.Resolve([1, 4], [2, 3]);

        Assert.Equal([1, 4], round.AttackerDice);
        Assert.Equal([2, 3], round.DefenderDice);
    }

    [Fact]
    public void Resolve_RejectsMismatchedDiceCounts()
    {
        Assert.Throws<ArgumentException>(() => BattleEngine.Resolve([1, 2], [3]));
        Assert.Throws<ArgumentException>(() => BattleEngine.Resolve([], []));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Resolve_RejectsDieValuesOutsideOneToSix(int badValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEngine.Resolve([badValue], [3]));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEngine.Resolve([3], [badValue]));
    }

    [Fact]
    public void Fight_BriefExample_LeavesAttackerNineAndDefenderOne()
    {
        // Brief p. 3: attacker 10 soldiers, defender 2; attacker rolls [1, 4], defender rolls [2, 3].
        var dice = new ScriptedDiceRoller(1, 4, 2, 3);

        var round = BattleEngine.Fight(attackerSoldiers: 10, defenderSoldiers: 2, dice);

        Assert.Equal([1, 4], round.AttackerDice);
        Assert.Equal([2, 3], round.DefenderDice);
        Assert.Equal(9, 10 - round.AttackerLosses);
        Assert.Equal(1, 2 - round.DefenderLosses);
        Assert.Equal(0, dice.Remaining);
    }

    // Brief p. 3: India (9) -> 4, Vatican City (1) -> 2.
    [Theory]
    [InlineData(9, 4)]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    [InlineData(6, 3)]
    [InlineData(8, 4)]
    public void ConquestBonus_IsMaxOfTwoAndHalfSoldierValue(int soldierValue, int expected)
    {
        Assert.Equal(expected, BattleEngine.ConquestBonus(soldierValue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ConquestBonus_RejectsSoldierValuesOutsideBands(int soldierValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleEngine.ConquestBonus(soldierValue));
    }

    [Fact]
    public void EachDicePair_IsFair_15_15_6_OutOf36()
    {
        // Underpins the NPC win-probability model: every pair is a coin flip once ties are ignored.
        int attackerWins = 0, defenderWins = 0, ties = 0;
        for (var a = 1; a <= 6; a++)
        for (var d = 1; d <= 6; d++)
        {
            var round = BattleEngine.Resolve([a], [d]);
            if (round.DefenderLosses == 1) attackerWins++;
            else if (round.AttackerLosses == 1) defenderWins++;
            else ties++;
        }

        Assert.Equal((15, 15, 6), (attackerWins, defenderWins, ties));
    }
}
