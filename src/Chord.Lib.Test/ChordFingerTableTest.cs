using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using FluentAssertions;
using xRetry;
using Xunit;

namespace Chord.Lib.Test.FingerTableTests;

public class TableCreationTest
{
    // this test can occasionally fail because keys are initialized uniform randomly
    // it's very, very unlikely that there's a bad seed 5 times in a row
    [RetryFact(5)]
    public async Task Test_ShouldCreateFingerTable_GivenAnEstablishedChordNetwork()
    {
        // arrange
        const int MAX_KEY = 1000000;
        const int NUM_NODES = 1000;
        var chordNodes = Enumerable.Range(0, NUM_NODES)
            .Select(i => new ChordEndpoint(null, null, MAX_KEY))
            .ToList();
        var (local, successor) = (chordNodes[0], chordNodes[1]);
        var networkNodes = chordNodes.Except(new [] { local }).ToList();

        // act
        var fingerTable = new ChordFingerTable(
            (k, t) => Task.FromResult(
                networkNodes.Where(x => x.NodeId >= k).MinBy(x => x.NodeId)
                ?? networkNodes.MinBy(x => x.NodeId) as IChordEndpoint),
            local,
            successor);
        await fingerTable.BuildTable();

        // assert
        var actualNormFingerIds = fingerTable.AllFingers
            .Select(x => x.NodeId - local.NodeId)
            .OrderBy(x => x);
        var optimalNormFingerIds = Enumerable.Range(0, (int)BigInteger.Log(MAX_KEY, 2))
            .Select(i => new ChordKey(BigInteger.Pow(2, i), MAX_KEY));

        actualNormFingerIds.Should().NotBeEmpty();
        optimalNormFingerIds.Should().Match(optFingers => optFingers
            .SkipWhile(x => x.Id <= 5 * MAX_KEY / NUM_NODES)
            .All(opt => actualNormFingerIds
                .Select(act => BigInteger.Abs(act.Id - opt.Id))
                .MinBy(x => x) <= opt.Id / 2));
    }
}

public class FingerLookupTest
{
    #region Init

    private ChordFingerTable initFingerTable(
        IList<IChordEndpoint> endpoints)
    {
        var local = endpoints[0];
        var successor = endpoints[1];

        var fingerTable = new ChordFingerTable(
            (k, t) => Task.FromResult(
                endpoints.MinBy(x => x.NodeId - k)),
            local,
            successor);

        fingerTable.BuildTable().Wait();
        return fingerTable;
    }

    const int KeySpace = 1000000;

    #endregion Init

    Func<BigInteger, IChordEndpoint> endpointOfKey = (k) => new ChordEndpoint(
            new ChordKey(k, KeySpace), ChordHealthStatus.Idle, null, null);

    [Fact]
    public void Test_ShouldFindSuccessor_WhenKeyIsBetweenLocalAndSuccessor()
    {
        var fingerTable = initFingerTable(
            new List<IChordEndpoint>() {
                endpointOfKey(1000),
                endpointOfKey(10000),
                endpointOfKey(100000),
                endpointOfKey(10),
                endpointOfKey(100),
            }
        );

        var lookupKey = new ChordKey(1001, KeySpace);
        var lookupResult = fingerTable.FindBestFinger(lookupKey);

        lookupResult.NodeId.Should().BeEquivalentTo(new ChordKey(10000, KeySpace));
    }

    [Fact]
    public void Test_ShouldFindSuccessor_WhenKeyIsBetweenLocalAndSuccessorWithRangeOverflow()
    {
        var fingerTable = initFingerTable(
            new List<IChordEndpoint>() {
                endpointOfKey(100000),
                endpointOfKey(10),
                endpointOfKey(100),
                endpointOfKey(1000),
                endpointOfKey(10000),
            }
        );

        var lookupKey = new ChordKey(100001, KeySpace);
        var lookupResult = fingerTable.FindBestFinger(lookupKey);

        lookupResult.NodeId.Should().BeEquivalentTo(new ChordKey(10, KeySpace));
    }

    [Fact]
    public void Test_ShouldFindSuccessorAsClostestPredecessor_WhenKeyIsNotBetweenLocalAndSuccessor()
    {
        var fingerTable = initFingerTable(
            new List<IChordEndpoint>() {
                endpointOfKey(1000),
                endpointOfKey(10000),
                endpointOfKey(100000),
                endpointOfKey(10),
                endpointOfKey(100),
            }
        );

        var lookupKey = new ChordKey(10001, KeySpace);
        var lookupResult = fingerTable.FindBestFinger(lookupKey);

        lookupResult.NodeId.Should().BeEquivalentTo(new ChordKey(10000, KeySpace));
    }

    [Fact]
    public void Test_ShouldFindClostestPredecessor_WhenKeyIsNotBetweenLocalAndSuccessor()
    {
        var fingerTable = initFingerTable(
            new List<IChordEndpoint>() {
                endpointOfKey(1000),
                endpointOfKey(10000),
                endpointOfKey(100000),
                endpointOfKey(10),
                endpointOfKey(100),
            }
        );

        var lookupKey = new ChordKey(100001, KeySpace);
        var lookupResult = fingerTable.FindBestFinger(lookupKey);

        lookupResult.NodeId.Should().BeEquivalentTo(new ChordKey(100000, KeySpace));
    }

    [Fact]
    public void Test_ShouldFindClostestPredecessor_WhenKeyIsNotBetweenLocalAndSuccessorWithRangeOverflow()
    {
        var fingerTable = initFingerTable(
            new List<IChordEndpoint>() {
                endpointOfKey(1000),
                endpointOfKey(10000),
                endpointOfKey(100000),
                endpointOfKey(10),
                endpointOfKey(100),
            }
        );

        var lookupKey = new ChordKey(11, KeySpace);
        var lookupResult = fingerTable.FindBestFinger(lookupKey);

        lookupResult.NodeId.Should().BeEquivalentTo(new ChordKey(10, KeySpace));
    }
}
