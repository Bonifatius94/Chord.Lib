namespace Chord.Lib;

using KeyLookupFunc = Func<ChordKey, CancellationToken, Task<IChordEndpoint>>;

public class ChordNodeState
{
    public ChordNodeState(IChordEndpoint local, Func<int> getFingerCount = null)
    {
        Local = local;
        this.getFingerCount = getFingerCount;
    }

    private Func<int> getFingerCount;

    public IChordEndpoint Local { get; private set; }
    public IChordEndpoint Successor { get; private set; }
    public IChordEndpoint Predecessor { get; private set; }

    public int FingerCount => getFingerCount?.Invoke() ?? 0;

    public void UpdateSuccessor(IChordEndpoint newSuccessor)
        => Successor = newSuccessor;

    public void UpdatePredecessor(IChordEndpoint newPredecessor)
        => Predecessor = newPredecessor;
}

public interface IChordNetworkRouter
{
    public int FingerCount { get; }
    IChordEndpoint FindBestFinger(ChordKey key);
}

public class ChordFingerTable : IChordNetworkRouter
{
    #region Init

    public ChordFingerTable(
        KeyLookupFunc lookupKeyAsync,
        ChordNodeState state,
        int updateTableTimeoutMillis = 600)
        : this(new ConcurrentDictionary<ChordKey, IChordEndpoint>(),
            lookupKeyAsync, state, updateTableTimeoutMillis) { }

    public ChordFingerTable(
        IDictionary<ChordKey, IChordEndpoint> fingers,
        KeyLookupFunc lookupKeyAsync,
        ChordNodeState state,
        int updateTableTimeoutMillis = 600)
    {
        this.fingerTable = new ConcurrentDictionary<ChordKey, IChordEndpoint>(fingers);
        this.lookupKeyAsync = lookupKeyAsync;
        this.state = state;
        this.updateTableTimeoutMillis = updateTableTimeoutMillis;
    }

    private readonly KeyLookupFunc lookupKeyAsync;
    private readonly int updateTableTimeoutMillis;

    #endregion Init

    private ChordNodeState state;
    private IChordEndpoint Local => state.Local;
    private IChordEndpoint Successor => state.Successor;
    private IChordEndpoint Predecessor => state.Predecessor;

    private IDictionary<ChordKey, IChordEndpoint> fingerTable = 
        new ConcurrentDictionary<ChordKey, IChordEndpoint>();

    public IEnumerable<IChordEndpoint> AllFingers
        => fingerTable.Values;

    public int FingerCount => AllFingers?.Count() ?? 0;

    #region Forwarding

    /// <summary>
    /// Find the Chord endpoint to forward the key lookup request to.
    /// </summary>
    /// <param name="lookupKey">The key to be looked up.</param>
    /// <returns>a Chord endpoint.</returns>
    public IChordEndpoint FindBestFinger(ChordKey lookupKey)
    {
        if (!fingerTable.Any() || Successor == null)
            throw new InvalidOperationException("Finger table is still uninitialized!");

        if (Successor.NodeId - Local.NodeId >= lookupKey - Local.NodeId)
            return Successor;

        var closestPredecessor = fingerTable.Values.MaxBy(x => x.NodeId - lookupKey);
        bool isSuccessorCloser = Successor.NodeId - lookupKey > closestPredecessor.NodeId - lookupKey;
        return isSuccessorCloser ? Successor : closestPredecessor;
    }

    #endregion Forwarding

    #region TableCreation

    /// <summary>
    /// Scan the network for other Chord nodes managing given keys
    /// and remember those endpoints as fingers.
    /// 
    /// The keys to be looked up are not follow the pattern of exponentially
    /// growing distances between the keys up to a key roughly at the opposite
    /// side of the chord token-ring.
    /// </summary>
    /// <param name="token">A cancellation token to cancel the procedure gracefully.</param>
    public async Task BuildTable(CancellationToken token)
    {
        var fingerKeys = optimalFingerKeys(Local);
        var newFingers = await findFingersAsync(fingerKeys, token);
        fingerTable = new ConcurrentDictionary<ChordKey, IChordEndpoint>(
            newFingers.ToDictionary(x => x.NodeId));

        // TODO: what about Chord ring fusions?!
        //       -> re-scan the network with IExplorableEndpointProvider
    }

    // get the ids 2^i for i in { 0, ..., log2(maxId) - 1 } to be looked up
    private IList<ChordKey> optimalFingerKeys(IChordEndpoint local)
        => Enumerable.Range(0, (int)BigInteger.Log(local.NodeId.KeySpace, 2))
            .Select(i => new ChordKey(BigInteger.Pow(2, i), local.NodeId.KeySpace))
            .Select(key => key + local.NodeId)
            .ToList();

    private async Task<IEnumerable<IChordEndpoint>> findFingersAsync(
        IList<ChordKey> fingerKeys, CancellationToken token)
    {
        var lookupTasks = new Task<IChordEndpoint>[0];

        await Task.Run(() => {

            var timeoutTask = Task.Delay(updateTableTimeoutMillis, token);
            var tokenSource = new CancellationTokenSource();
            lookupTasks = fingerKeys
                .Select(x => lookupKeyAsync(x, tokenSource.Token))
                .ToArray();

            var allLookupsCompleteTask = Task.WhenAll(lookupTasks);
            int firstTask = Task.WaitAny(timeoutTask, allLookupsCompleteTask);
            if (firstTask == 0)
                tokenSource.Cancel();
        });

        return lookupTasks
            .Where(x => x.Status == TaskStatus.RanToCompletion)
            .Select(x => x.Result)
            .DistinctBy(x => x.NodeId)
            .ToList();
    }

    #endregion TableCreation
}
