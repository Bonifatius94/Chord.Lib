using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Chord.Lib.Test.ChordKeyTest;

public class EqualityTest
{
    [Fact]
    public void Test_ShouldBeEqual_WhenIdsAreTheSame()
    {
        var key1 = new ChordKey(10, 20);
        var key2 = new ChordKey(10, 20);
        key1.Should().BeEquivalentTo(key2);
    }

    [Fact]
    public void Test_KeysShouldEqual_WhenIdsAreTheSame()
    {
        var key1 = new ChordKey(10, 20);
        var key2 = new ChordKey(9, 20);
        key1.Should().NotBeEquivalentTo(key2);
    }
}

public class AddSubTest
{
    [Fact]
    public void Test_ShouldAddNormal_WhenNoModulOverflow()
    {
        var key1 = new ChordKey(10, 20);
        var key2 = new ChordKey(9, 20);
        var addResult = key1 + key2;
        addResult.Should().Be(new ChordKey(19, 20));
    }

    [Fact]
    public void Test_ShouldDivideModMaxId_WhenAddWithModulOverflow()
    {
        var key1 = new ChordKey(10, 20);
        var key2 = new ChordKey(10, 20);
        var addResult = key1 + key2;
        addResult.Should().Be(new ChordKey(0, 20));
    }

    [Fact]
    public void Test_ShouldSubNormal_WhenNoModulOverflow()
    {
        var key1 = new ChordKey(10, 20);
        var key2 = new ChordKey(9, 20);
        var addResult = key1 - key2;
        addResult.Should().Be(new ChordKey(1, 20));
    }

    [Fact]
    public void Test_ShouldDivideModMaxId_WhenSubWithModulOverflow()
    {
        var key1 = new ChordKey(9, 20);
        var key2 = new ChordKey(10, 20);
        var addResult = key1 - key2;
        addResult.Should().Be(new ChordKey(19, 20));
    }
}

public class PickRandomTest
{
    [Fact]
    public void Test_ShouldProduceDifferentKeys_WhenCreatingRange()
    {
        const long KEY_SPACE = long.MaxValue;
        var keys = Enumerable.Range(0, 10000)
            .Select(x => ChordKey.PickRandom()).ToList();
        keys.Distinct().Should().HaveCount(keys.Count());
        keys.Should().Match(x => x.All(key => key.Id >= 0 && key.Id < KEY_SPACE));
    }

    [Fact]
    public void Test_ShouldProduceEqualDistKeys_WhenCreatingRange()
    {
        const int KEY_SPACE = 1000;
        const int TOTAL_KEYS = 100000;

        var keys = Enumerable.Range(0, TOTAL_KEYS)
            .Select(x => ChordKey.PickRandom(KEY_SPACE)).ToList();

        double entropy = keys
            .GroupBy(x => x)
            .ToDictionary(x => x.Key, x => (double)x.Count() / TOTAL_KEYS)
            .Select(x => - x.Value * Math.Log(x.Value, KEY_SPACE))
            .Sum();

        keys.Should().Match(x => x.All(key => key.Id >= 0 && key.Id < KEY_SPACE));
        entropy.Should().BeGreaterThan(0.95);
    }
}
