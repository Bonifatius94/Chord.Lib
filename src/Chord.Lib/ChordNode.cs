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
        IChordClient client,
        IChordPayloadWorker payloadWorker,
        ChordNodeConfiguration config,
        ILogger logger = null)
    {
        this.config = config;
        this.payloadWorker = payloadWorker;

        sender = new ChordRequestSender(client, () => fingerTable);
        receiver = new ChordRequestReceiver(() => fingerTable, sender, payloadWorker, logger);
        backgroundTaskCallback = new CancellationTokenSource();
    }

    private readonly ChordNodeConfiguration config;
    private readonly ChordRequestReceiver receiver;
    private readonly ChordRequestSender sender;
    private readonly IChordPayloadWorker payloadWorker;
    private readonly CancellationTokenSource backgroundTaskCallback;
    private ChordFingerTable fingerTable;

    #endregion Init

    public ChordKey NodeId => fingerTable.Local.NodeId;

    public async Task<IChordResponseMessage> ProcessRequest(
            IChordRequestMessage request)
        => await receiver.ProcessAsync(request);

    public async Task JoinNetwork(IChordEndpoint local, IChordBootstrapper bootstrapper)
    {
        // TODO: decouple the error handling with Action callbacks

        if (local.State != ChordHealthStatus.Starting)
            throw new InvalidOperationException(
                "Node needs to be in 'Starting' state!");

        // phase 0: find an entrypoint into the chord network
        var bootstrap = await bootstrapper.FindBootstrapNode();
        if (bootstrap == null)
            throw new InvalidOperationException(
                "Cannot find a bootstrap node! Please try to join again!");

        // phase 1: determine the successor by a key lookup
        var successor = await sender.IntiateSuccessor(bootstrap, local);

        // phase 2: initiate the join process
        var response = await sender.InitiateNetworkJoin(local, successor);
        if (!response.ReadyForDataCopy)
            throw new InvalidOperationException(
                "Network join failed! Cannot copy payload data!");
        await payloadWorker.PreloadData(successor);

        // phase 4: finalize the join process
        response = await sender.CommitNetworkJoin(local, successor);
        if (!response.CommitSuccessful)
            throw new InvalidOperationException(
                "Joining the network unexpectedly failed! Please try again!");

        // apply the node settings
        local.UpdateState(ChordHealthStatus.Idle);
        fingerTable = new ChordFingerTable(
            response.CachedFingerTable.ToDictionary(x => x.NodeId),
            (k, t) => sender.SearchEndpointOfKey(k, local),
            local, successor, response.Predecessor);

        if (!fingerTable.AllFingers.Any())
            await fingerTable.BuildTable();

        // start the health monitoring / finger table update
        // procedures as scheduled background tasks
        createBackgroundTasks(backgroundTaskCallback.Token).Start();
    }

    public async Task LeaveNetwork()
    {
        // phase 1: initiate the leave process
        fingerTable.Local.UpdateState(ChordHealthStatus.Leaving);

        // TODO: this might not be necessary when following an event sourcing approach
        //       that locks the state when processing mutable events
        var local = fingerTable.Local.DeepClone();
        var successor = fingerTable.Successor.DeepClone();
        var predecessor = fingerTable.Predecessor.DeepClone();

        var response = await sender.InitiateNetworkLeave(local, successor);
        if (!response.ReadyForDataCopy)
            throw new InvalidOperationException(
                "Network leave failed! Cannot copy payload data!");

        // phase 2: copy all existing payload data from this node to the successor
        await payloadWorker.BackupData(successor);

        // phase 3: finalize the leave process
        response = await sender.CommitNetworkLeave(local, successor, predecessor);
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
        var monitorTask = Task.Run(() => {

            while (!token.IsCancellationRequested)
            {
                Task.Delay(config.MonitorHealthSchedule * 1000).Wait();
                monitorFingerHealth(token);
            }
        });

        // run the 'update table' task on a regular time schedule
        var updateTableTask = Task.Run(() => {

            while (!token.IsCancellationRequested)
            {
                Task.Delay(config.UpdateTableSchedule * 1000).Wait();
                fingerTable.BuildTable().Wait();
            }
        });

        // wait until both tasks exited gracefully by cancellation
        await Task.WhenAll(new Task[] { monitorTask, updateTableTask });
    }

    private void monitorFingerHealth(CancellationToken token)
    {
        var cachedFingers = fingerTable.AllFingers.ToList();

        // perform a first health check for all finger nodes
        updateHealth(cachedFingers, config.HealthCheckTimeoutMillis, ChordHealthStatus.Questionable);
        if (token.IsCancellationRequested)
            return;

        // perform a second health check for all questionable finger nodes
        var questionableFingers = cachedFingers
            .Where(x => x.State == ChordHealthStatus.Questionable).ToList();
        updateHealth(questionableFingers, config.HealthCheckTimeoutMillis / 2, ChordHealthStatus.Dead);
        if (token.IsCancellationRequested)
            return;
    }

    private void updateHealth(
        List<IChordEndpoint> fingers,
        int timeoutSecs, 
        ChordHealthStatus failStatus)
    {
        // run health checks for each finger in parallel
        var healthCheckTasks = fingerTable.AllFingers
            .Select(receiver => Task.Run(async () => new { 
                NodeId = receiver.NodeId,
                Health = await sender.HealthCheck(
                    fingerTable.Local, receiver, failStatus, timeoutSecs)
            })).ToArray();
        Task.WaitAll(healthCheckTasks);

        // collect health check results
        var healthStates = healthCheckTasks.Select(x => x.Result)
            .ToDictionary(x => x.NodeId, x => x.Health);

        // update finger states
        fingers.ForEach(finger => finger.UpdateState(healthStates[finger.NodeId]));
    }

    #endregion BackgroundTasks
}
