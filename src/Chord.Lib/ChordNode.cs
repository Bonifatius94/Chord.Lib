using Microsoft.Extensions.Logging;

namespace Chord.Lib;

public class ChordNodeConfiguration
{
    public int MonitorHealthSchedule { get; set; } = 600;
    public int UpdateTableSchedule { get; set; } = 600;
    public int HealthCheckTimeoutMillis { get; set; } = 1000;
}

/// <summary>
/// This class provides all core functionality of the chord protocol.
/// 
/// Note that this is supposed to be a logical chord endpoint abstracting
/// the actual network traffic. If you want to use this code properly, 
/// you need to provide a callback function for exchanging messages
/// between chord endpoints, etc. (see constructor).
/// </summary>
public class ChordNode
{
    #region Init

    /// <summary>
    /// Create a new chord node with the given request callback,
    /// upper resource id bound and timeout configuration.
    /// </summary>
    /// <param name="client">An endpoint that's capable of
    /// sending Chord requests to other nodes.</param>
    /// <param name="config">A set of configuration settings
    /// controlling the node behavior.</param>
    public ChordNode(
        IChordEndpoint local,
        IChordClient client,
        IChordPayloadWorker payloadWorker,
        ChordNodeConfiguration config,
        ILogger logger = null)
    {
        this.config = config;
        this.payloadWorker = payloadWorker;
        nodeState = new ChordNodeState(local);
        fingerTable = new ChordFingerTable((k, t) => null, nodeState);
        sender = new ChordRequestSender(client, fingerTable);
        receiver = new ChordRequestReceiver(nodeState, sender, payloadWorker, logger);
        processor = new ChordEventProcessor(receiver);
        backgroundTaskCallback = new CancellationTokenSource();
    }

    private readonly ChordNodeState nodeState;
    private readonly ChordNodeConfiguration config;
    private readonly ChordRequestReceiver receiver;
    private readonly ChordRequestSender sender;
    private readonly IChordPayloadWorker payloadWorker;
    private readonly ChordEventProcessor processor;
    private readonly CancellationTokenSource backgroundTaskCallback;
    private ChordFingerTable fingerTable;

    #endregion Init

    public IChordEndpoint Local => nodeState.Local;
    public IChordEndpoint Successor => nodeState.Successor;
    public IChordEndpoint Predecessor => nodeState.Predecessor;
    public ChordKey NodeId => Local.NodeId;
    public ChordHealthStatus NodeState => Local.State;

    public async Task<IChordResponseMessage> ProcessRequest(
        IChordRequestMessage request, CancellationToken token)
    {
        var chordEvent = new ChordEvent(request, ChordEventType.Incoming);
        processor.Enqueue(chordEvent);
        return await chordEvent.OnProcessingComplete(token);
    }

    public async Task JoinNetwork(
        IChordBootstrapper bootstrapper,
        CancellationToken token)
    {
        // TODO: decouple the error handling with Action callbacks

        var local = Local;
        if (local.State != ChordHealthStatus.Starting)
            return;

        // phase 0: find an entrypoint into the chord network
        var bootstrap = await bootstrapper.FindBootstrapNode(sender, local);
        if (bootstrap == null)
            throw new InvalidOperationException(
                "Cannot find a bootstrap node! Please try to join again!");

        // phase 1: determine the successor by a key lookup
        var successor = await sender.IntiateSuccessor(bootstrap, local, token);
        if (successor == null)
            throw new InvalidOperationException(
                "Could not find a successor. Please try again!");

        // phase 2: initiate the join process
        var response = await sender.InitiateNetworkJoin(local, successor, token);
        if (!response.ReadyForDataCopy)
            throw new InvalidOperationException(
                "Network join failed! Cannot copy payload data!");
        await payloadWorker.PreloadData(successor);

        // phase 4: finalize the join process
        response = await sender.CommitNetworkJoin(local, successor, token);
        if (!response.CommitSuccessful)
            throw new InvalidOperationException(
                "Joining the network unexpectedly failed! Please try again!");

        // apply the node settings
        local.UpdateState(ChordHealthStatus.Idle);
        nodeState.UpdateSuccessor(successor);
        nodeState.UpdatePredecessor(response.Predecessor);
        fingerTable = new ChordFingerTable(
            response.CachedFingerTable.ToDictionary(x => x.NodeId),
            (k, t) => sender.SearchEndpointOfKey(k, local, t),
            nodeState);

        if (!fingerTable.AllFingers.Any())
            await fingerTable.BuildTable();

        // start the health monitoring / finger table update
        // procedures as scheduled background tasks
        #pragma warning disable CS4014 // call is not awaited
        createBackgroundTasks(backgroundTaskCallback.Token);
        #pragma warning restore CS4014
    }

    public async Task LeaveNetwork(CancellationToken token)
    {
        // phase 1: initiate the leave process
        Local.UpdateState(ChordHealthStatus.Leaving);

        // TODO: this might not be necessary when following an event sourcing approach
        //       that locks the state when processing mutable events
        var local = Local.DeepClone();
        var successor = nodeState.Successor.DeepClone();
        var predecessor = nodeState.Predecessor.DeepClone();

        var response = await sender.InitiateNetworkLeave(local, successor, token);
        if (!response.ReadyForDataCopy)
            throw new InvalidOperationException(
                "Network leave failed! Cannot copy payload data!");

        // phase 2: copy all existing payload data from this node to the successor
        await payloadWorker.BackupData(successor);

        // phase 3: finalize the leave process
        response = await sender.CommitNetworkLeave(
            local, successor, predecessor, token);
        if (!response.CommitSuccessful)
            throw new InvalidOperationException(
                "Leaving the network unexpectedly failed! Please try again!");
        // TODO: what happens if the leave procedure fails?!

        // shut down all background tasks (health monitoring and finger table updates)
        backgroundTaskCallback.Cancel();
    }

    #region BackgroundTasks

    private async Task createBackgroundTasks(CancellationToken token)
    {
        // run the 'monitor health' task on a regular time schedule
        var monitorTask = async () => {

            while (!token.IsCancellationRequested)
            {
                Task.Delay(config.MonitorHealthSchedule * 1000).Wait();
                await monitorFingerHealth(token);
                if (token.IsCancellationRequested)
                    return;
            }
        };

        // run the 'update table' task on a regular time schedule
        var updateTableTask = async () => {

            while (!token.IsCancellationRequested)
            {
                Task.Delay(config.UpdateTableSchedule * 1000).Wait();
                await fingerTable.BuildTable();
            }
        };

        // wait until both tasks exited gracefully by cancellation
        await Task.WhenAll(new Task[] { monitorTask(), updateTableTask() });
    }

    private static readonly ISet<ChordHealthStatus> unhealthyStates =
        new HashSet<ChordHealthStatus>() {
            ChordHealthStatus.Questionable,
            ChordHealthStatus.Dead
        };

    private async Task monitorFingerHealth(CancellationToken token)
    {
        var cachedFingers = fingerTable.AllFingers.ToList();

        var firstHealthCheck = async (IEnumerable<IChordEndpoint> fingers, CancellationToken token)
            => await queryHealthStates(
                fingers,
                config.HealthCheckTimeoutMillis,
                ChordHealthStatus.Questionable,
                token);

        var secondHealthCheck = async (IEnumerable<IChordEndpoint> fingers, CancellationToken token)
            => await queryHealthStates(
                fingers,
                config.HealthCheckTimeoutMillis,
                ChordHealthStatus.Dead,
                token);

        var healthStatesOfFingers = await firstHealthCheck(cachedFingers, token);
        foreach ((var finger, var newState) in healthStatesOfFingers)
            finger.UpdateState(newState);

        var questionableFingers = healthStatesOfFingers
            .Where(x => unhealthyStates.Contains(x.Item2))
            .Select(x => x.Item1)
            .ToList();

        healthStatesOfFingers = await secondHealthCheck(questionableFingers, token);
        foreach ((var finger, var newState) in healthStatesOfFingers)
            finger.UpdateState(newState);
    }

    private async Task<IEnumerable<(IChordEndpoint, ChordHealthStatus)>> queryHealthStates(
        IEnumerable<IChordEndpoint> fingers,
        int timeoutSecs, 
        ChordHealthStatus failStatus,
        CancellationToken token)
    {
        var healthCheckTasks = fingers
            .Select(async finger => (
                Endpoint: finger,
                HealthState: await sender.HealthCheck(
                    Local, finger, token, failStatus, timeoutSecs)
            ));

        return await Task.WhenAll(healthCheckTasks);
    }

    #endregion BackgroundTasks
}
