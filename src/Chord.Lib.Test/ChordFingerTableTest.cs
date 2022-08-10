using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using FluentAssertions;
using xRetry;
using Xunit;

namespace Chord.Lib.Test.FingerTableTests;

public class ConcurrentFingerInsertTest
{
    [Fact]
    public void Test_ShouldNotCrash_WhenInsertingDuplicateFingersConcurrently()
    {
        const int MAX_KEY = 10000;
        var fingersToInsert = Enumerable.Range(0, MAX_KEY).Concat(Enumerable.Range(0, MAX_KEY))
            .Select(i => new ChordEndpoint() { NodeId = new ChordKey(i, MAX_KEY) })
            .ToList();

        var table = new ChordFingerTable((k, t) => null, MAX_KEY);
        var insertTasks = fingersToInsert
            .Select(finger => Task.Run(() => table.InsertFinger(finger)))
            .ToArray();
        Task.WaitAll(insertTasks);

        table.AllFingers.Should().HaveCount(
            fingersToInsert.Select(x => x.NodeId).Distinct().Count());
    }
}

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
            .Select(i => new ChordEndpoint() { NodeId = ChordKey.PickRandom(MAX_KEY) })
            .ToList();
        var local = chordNodes.First();
        var networkNodes = chordNodes.Except(new [] { local }).ToList();

        // act
        var fingerTable = new ChordFingerTable(
            (k, t) => Task.FromResult(
                networkNodes.Where(x => x.NodeId >= k).ArgMin(x => x.NodeId)
                ?? networkNodes.ArgMin(x => x.NodeId) as IChordEndpoint),
            MAX_KEY);
        await fingerTable.BuildTable(local.NodeId);

        // assert
        var actualNormFingerIds = fingerTable.AllFingers
            .Select(x => x.NodeId - local.NodeId)
            .OrderBy(x => x);
        var optimalNormFingerIds = Enumerable.Range(0, (int)BigInteger.Log(MAX_KEY, 2))
            .Select(i => new ChordKey(BigInteger.Pow(2, i), MAX_KEY));

        optimalNormFingerIds.Should().Match(optFingers => optFingers
            .SkipWhile(x => x.Id <= 5000)
            .All(opt => actualNormFingerIds
                .Select(act => BigInteger.Abs(act.Id - opt.Id))
                .ArgMin(x => x) <= opt.Id / 2));
    }
}

// public class FingerLookupTest
// {
//     #region Init

//     private async Task<ChordFingerTable> initTable(
//         BigInteger MAX_KEY,
//         IChordEndpoint local,
//         IEnumerable<IChordEndpoint> networkNodes)
//     {
//         var fingerTable = new ChordFingerTable(
//             (k, t) => Task.FromResult(
//                 networkNodes.FirstOrDefault(x => x.NodeId >= k)
//                 ?? networkNodes.ArgMin(x => x.NodeId) as IChordEndpoint),
//             MAX_KEY);
//         await fingerTable.BuildTable(local.NodeId);
//         return fingerTable;
//     }

//     #endregion Init

//     [Fact]
//     public async Task Test_ShouldProvideCorrectEndpoints_WhenLookingUpChordKeys()
//     {
//         // arrange
//         const int MAX_KEY = 1000000;
//         const int NUM_NODES = 1000;
//         const int NUM_LOOKUPS = 1000;
//         var chordNodes = Enumerable.Range(0, NUM_NODES)
//             .Select(i => new ChordEndpoint() { NodeId = ChordKey.PickRandom(MAX_KEY) })
//             .ToList();
//         var (local, successor) = (chordNodes[0], chordNodes[1]);
//         var networkNodes = chordNodes.Except(new [] { local }).ToList();
//         var fingerTable = await initTable(MAX_KEY, local, networkNodes);

//         // act
//         var keysToLookUp = Enumerable.Range(0, NUM_LOOKUPS)
//             .Select(x => ChordKey.PickRandom(MAX_KEY)).ToList();
//         var endpointOfKey = keysToLookUp
//             .Select(k => fingerTable.FindBestFinger(k, successor.NodeId));

//         // assert

//     }
// }

public static class ArgMinEx
{
    public static T ArgMin<T>(
        this IEnumerable<T> items,
        Func<T, IComparable> compAttrSelector)
    {
        if (!items.Any())
            throw new ArgumentException("Empty list has no minimum!");

        var minItem = items.First();

        foreach (var item in items)
           if (compAttrSelector(item).CompareTo(compAttrSelector(minItem)) < 0)
               minItem = item;

        return minItem;
    }
}
