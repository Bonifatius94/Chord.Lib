namespace Chord.Lib;

using KeyLookupFunc = Func<ChordKey, CancellationToken, Task<IChordEndpoint>>;

public class ChordFingerTable
{
    #region Init

    // TODO: pass the local node id by constructor

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

    #endregion Init

    // TODO: add an attribute representing the successor endpoint (a special finger)

    private IDictionary<ChordKey, IChordEndpoint> fingerTable = 
        new ConcurrentDictionary<ChordKey, IChordEndpoint>();

    public IEnumerable<IChordEndpoint> AllFingers
        => fingerTable.Values;

    public void InsertFinger(IChordEndpoint finger)
        => fingerTable.TryAdd(finger.NodeId, finger);
        // TODO: think of implementing the successor as a separate attribute

    #region FingerForwarding

    /// <summary>
    /// Find the chord endpoint managing the given key.
    /// </summary>
    /// <param name="key">The key to be looked up.</param>
    /// <param name="successorKey">The local node's successor id.</param>
    /// <returns></returns>
    public IChordEndpoint FindBestFinger(ChordKey key, ChordKey successorKey)
    {
        if (!fingerTable.Any())
            return null;

        // termination case: forward to the successor if it is the manager of the key
        // recursion case: forward to the closest predecessing finger of the key
        //     -> eventually, the predecessor of the node searched is found
        var fingerKey = successorKey >= key ? successorKey
            : findClosestPredecessor(key);

        // info: this code assumes that the successor endpoint is inserted as finger
        // TODO: expose the special role of the successor a bit more explicitly
        return fingerTable.ContainsKey(fingerKey) ? fingerTable[fingerKey] : null;
    }

    private ChordKey findClosestPredecessor(ChordKey key)
        => fingerTable.Keys.Select(x => x - key).Max() + key;
        // TODO: this seems to be wrong ...

    #endregion FingerForwarding

    #region FingerTable

    /// <summary>
    /// Scan the network for other Chord nodes managing given keys
    /// and remember those endpoints as fingers.
    /// 
    /// The keys to be looked up are not follow the pattern of exponentially
    /// growing distances between the keys up to a key roughly at the opposite
    /// side of the chord token-ring.
    /// </summary>
    /// <param name="localId">The local endpoint's id.</param>
    /// <param name="token">A cancellation token to cancel the procedure gracefully.</param>
    public async Task BuildTable(ChordKey localId, CancellationToken? token = null)
    {
        // get the ids 2^i for i in { 0, ..., log2(maxId) - 1 } to be looked up
        var fingerKeys = Enumerable.Range(0, (int)BigInteger.Log(maxKey, 2))
            .Select(i => new ChordKey(BigInteger.Pow(2, i), maxKey))
            .Select(key => key + localId)
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
            .Select(x => x.Result)
            .DistinctBy(x => x.NodeId)
            .ToDictionary(x => x.NodeId);
        fingerTable = new ConcurrentDictionary<ChordKey, IChordEndpoint>(newFingers);

        // TODO: what about Chord ring fusions?!
    }

    #endregion FingerTable
}
