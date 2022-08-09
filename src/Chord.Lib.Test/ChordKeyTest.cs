using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Chord.Lib.Test.ChordKeyTest;

public class EqualityTest
{
    public static IEnumerable<object[]> equalIds =
        new List<object[]> {
            new object[] { new BigInteger(10), new BigInteger(10), new BigInteger(20) },
            new object[] { new BigInteger(10), new BigInteger(30), new BigInteger(20) },
            new object[] { new BigInteger(10), new BigInteger(-10), new BigInteger(20) },
            new object[] { new BigInteger(0L), new BigInteger(long.MaxValue), new BigInteger(long.MaxValue) },
        };

    public static IEnumerable<object[]> unequalIds =
        new List<object[]> {
            new object[] { new BigInteger(10), new BigInteger(0), new BigInteger(20) },
            new object[] { new BigInteger(10), new BigInteger(31), new BigInteger(20) },
            new object[] { new BigInteger(10), new BigInteger(-9), new BigInteger(20) },
            new object[] { new BigInteger(1ul), new BigInteger(1ul << 63), new BigInteger(1ul << 62) },
        };

    [Theory]
    [MemberData(nameof(equalIds))]
    public void Test_KeysShouldBeEqual_WhenIdsAreTheSameModulIdentity(
        BigInteger id1, BigInteger id2, BigInteger modul)
    {
        var key1 = new ChordKey(id1, modul);
        var key2 = new ChordKey(id2, modul);
        key1.Should().BeEquivalentTo(key2);
    }

    [Theory]
    [MemberData(nameof(unequalIds))]
    public void Test_KeysShouldNotBeEqual_WhenIdsAreNotTheSameModulIdentity(
        BigInteger id1, BigInteger id2, BigInteger modul
    )
    {
        var key1 = new ChordKey(id1, modul);
        var key2 = new ChordKey(id2, modul);
        key1.Should().NotBeEquivalentTo(key2);
    }
}

public class AddSubTest
{
    // TODO: add test cases with ids > 2^64

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
    public static IEnumerable<object[]> keySpaces =
        new List<object[]> {
            new object[] { new BigInteger(10) },
            new object[] { new BigInteger(ulong.MaxValue) },
            new object[] { new BigInteger(1) << 121 },
        };

    [Theory]
    [MemberData(nameof(keySpaces))]
    public void Test_ShouldProduceEquallyDistributedKeysWithinBounds_WhenCreatingRange(
        BigInteger keySpace)
    {
        const int TOTAL_KEYS = 10000;

        var keys = Enumerable.Range(0, TOTAL_KEYS)
            .Select(x => ChordKey.PickRandom(keySpace)).ToList();

        double entropy = keys
            .GroupBy(x => x)
            .ToDictionary(x => x.Key, x => (double)x.Count() / TOTAL_KEYS)
            .Select(x => - x.Value * BigInteger.Log((int)x.Value, (double)keySpace))
            .Sum();

        keys.Should().Match(x => x.All(key => key.Id >= 0 && key.Id < keySpace));
        entropy.Should().BeGreaterThan(0.95);
    }

    [Fact]
    public void Test_ShouldProduceBigKeysWithinBounds_WhenCreatingRange()
    {
        BigInteger KEY_SPACE = new BigInteger(1) << 121;
        var keys = Enumerable.Range(0, 10000)
            .Select(x => ChordKey.PickRandom(KEY_SPACE)).ToList();
        keys.Should().Match(x => x.All(key => key.Id >= 0 && key.Id < KEY_SPACE));
        keys.Should().Match(x => x.Any(key => key.Id >= new BigInteger(1) << 120));
    }
}
