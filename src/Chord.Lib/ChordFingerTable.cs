using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Lib;

using KeyLookupFunc = Func<ChordKey, CancellationToken, Task<IChordEndpoint>>;

public class ChordFingerTable
{
    public ChordFingerTable(
        KeyLookupFunc lookupKeyAsync,
        BigInteger maxKey,
        int updateTableTimeoutMillis = 600)
        : this(new ConcurrentDictionary<ChordKey, IChordEndpoint>(),
            lookupKeyAsync, maxKey, updateTableTimeoutMillis) { }

    public ChordFingerTable(
        IDictionary<ChordKey, IChordEndpoint> fingers,
        KeyLookupFunc lookupKeyAsync,
        BigInteger maxKey,
        int updateTableTimeoutMillis = 600)
    {
        this.lookupKeyAsync = lookupKeyAsync;
        this.maxKey = maxKey;
        this.updateTableTimeoutMillis = updateTableTimeoutMillis;
    }

    private readonly Func<ChordKey, CancellationToken, Task<IChordEndpoint>> lookupKeyAsync;
    private readonly BigInteger maxKey;
    private readonly int updateTableTimeoutMillis;

    private IDictionary<ChordKey, IChordEndpoint> fingerTable = 
        new ConcurrentDictionary<ChordKey, IChordEndpoint>();

    public IEnumerable<IChordEndpoint> AllFingers
        => fingerTable.Values;

    public void InsertFinger(IChordEndpoint finger)
        => fingerTable.Add(finger.NodeId, finger);

    public IChordEndpoint FindBestFinger(ChordKey key, ChordKey successorKey)
    {
        if (!fingerTable.Any())
            return null;

        // termination case: forward to the successor if it is the manager of the key
        // recursion case: forward to the closest predecessing finger of the key
        var fingerKey = successorKey >= key ? successorKey
            : FindClosestPredecessor(key);

        return fingerTable.ContainsKey(fingerKey) ? fingerTable[fingerKey] : null;
    }

    public ChordKey FindClosestPredecessor(ChordKey key)
        => fingerTable.Keys.Select(x => x + key).Max() - key;

    public async Task UpdateTable(ChordKey nodeId, CancellationToken? token = null)
    {
        // get the ids 2^i for i in { 0, ..., log2(maxId) - 1 } to be looked up
        var fingerKeys = Enumerable.Range(0, (int)BigInteger.Log(maxKey, 2) - 1)
            .Select(i => new ChordKey(BigInteger.Pow(2, i), maxKey))
            .Select(key => key + nodeId - nodeId)
            .ToList();

        var lookupTasks = new Task<IChordEndpoint>[0];

        // perform key lookups in parallel (pass a token for cancellation)
        // when a timeout occurs or the task is cancelled externally
        //   -> cancel all running tasks, keep already completed ones as is
        await Task.Run(() => {
            var tokenSource = new CancellationTokenSource();
            var timeoutTask = token == null
                ? Task.Delay(updateTableTimeoutMillis)
                : Task.Delay(updateTableTimeoutMillis, token.Value);
            lookupTasks = fingerKeys
                .Select(x => lookupKeyAsync(x, tokenSource.Token))
                .ToArray();
            var allLookupsCompleteTask = Task.WhenAll(lookupTasks);

            // cancel dangling lookup tasks after running into a timeout
            int firstTask = Task.WaitAny(timeoutTask, allLookupsCompleteTask);
            if (firstTask == 0)
                tokenSource.Cancel();
        });

        // create a new finger table by assigning the nodes that
        // responded to the lookup requests (including the successor)
        var newFingers = lookupTasks
            .Where(x => x.Status == TaskStatus.RanToCompletion)
            .Select(x => x.Result).ToDictionary(x => x.NodeId);
        fingerTable = new ConcurrentDictionary<ChordKey, IChordEndpoint>(newFingers);

        // TODO: what about Chord ring fusions?!
    }
}
