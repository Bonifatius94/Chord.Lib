using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Lib;

public class ChordNodeConfiguration
{
    public string IpAddress { get; set; }
    public string ChordPort { get; set; }
    public long MaxId { get; set; } = long.MaxValue;
    public int MonitorHealthSchedule { get; set; } = 600;
    public int UpdateTableSchedule { get; set; } = 600;
}

/// <summary>
/// This class provides all core functionality of the chord protocol.
/// 
/// Note that this is supposed to be a logical chord endpoint abstracting
/// the actual network traffic. If you want to use this code properly, 
/// you need to provide a callback function for exchanging messages
/// between chord endpoints, etc. (see constructor).
/// </summary>
public class ChordNode : IChordNode
{
    /// <summary>
    /// Create a new chord node with the given request callback, upper resource id bound and timeout configuration.
    /// </summary>
    /// <param name="sender">An endpoint that's capable of sending Chord requests to other nodes.</param>
    /// <param name="config">A set of configuration settings controlling the node behavior.</param>
    public ChordNode(IChordRequestSender sender, ChordNodeConfiguration config)
    {
        this.sender = sender;
        this.config = config;

        Local = new ChordEndpoint() {
            NodeId = ChordKey.PickRandom(config.MaxId),
            IpAddress = config.IpAddress,
            Port = config.ChordPort,
            State = ChordHealthStatus.Starting
        };

        requestProcessor = new ChordNodeRequestProcessor(this, sender);
        FingerTable = new ChordFingerTable(
            (x, y) => Task.FromResult((IChordEndpoint)null), config.MaxId);

        backgroundTaskCallback = new CancellationTokenSource();
    }

    private readonly ChordNodeConfiguration config;
    private readonly IChordRequestSender sender;
    private readonly ChordNodeRequestProcessor requestProcessor;
    private readonly CancellationTokenSource backgroundTaskCallback;

    public IChordEndpoint Local { get; private set; }
    public IChordEndpoint Successor  { get; private set; }
    public IChordEndpoint Predecessor  { get; private set; }
    public ChordFingerTable FingerTable { get; private set; }

    public ChordKey NodeId => Local.NodeId;

    public void UpdateSuccessor(IChordEndpoint newSuccessor)
        => Successor = newSuccessor;

    public async Task<IChordResponseMessage> ProcessRequest(
            IChordRequestMessage request)
        => await requestProcessor.ProcessAsync(request);

    public async Task JoinNetwork(Func<Task<IChordEndpoint>> findBootstrapNode)
    {
        // phase 0: find an entrypoint into the chord network

        var bootstrap = await findBootstrapNode();
        if (bootstrap == null) { throw new InvalidOperationException(
            "Cannot find a bootstrap node! Please try to join again!"); }

        // phase 1: determine the successor by a key lookup

        if (Local.State != ChordHealthStatus.Starting) { throw new InvalidOperationException(
            "Cannot join cluster! Make sure the node is in 'Starting' state!"); }

        do {

            Local.NodeId = ChordKey.PickRandom(config.MaxId);
            Successor = await LookupKey(NodeId, bootstrap);

        // continue until the generated node id is unique
        } while (Successor.NodeId == NodeId);

        // phase 2: initiate the join process

        // send a join initiation request to the successor
        var response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.InitNodeJoin,
                RequesterId = NodeId
            },
            Successor
        );

        if (!response.ReadyForDataCopy) {
            throw new InvalidOperationException("Network join failed! Cannot copy payload data!"); }

        // phase 3: copy all existing payload data this node is now responsible for

        // TODO: For production use, make sure to copy payload data, too.
        //       Keep in mind that the old successor is no more responsible for the ids
        //       being assigned to this newly created node. So there needs to be a kind of
        //       mechanism to copy the data first before enabling this node to avoid
        //       temporary data unavailability.

        // phase 4: finalize the join process

        // send a join initiation request to the successor
        // -> the successor sends an 'update successor' request to his predecessor
        // -> the predecessor's successor then points to this node
        // -> from then on this node is available to other Chord nodes on the network
        response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.CommitNodeJoin,
                RequesterId = NodeId,
                NewSuccessor = Local
            },
            Successor
        );

        // make sure the successor did successfully commit the join
        if (!response.CommitSuccessful) { throw new InvalidOperationException(
            "Joining the network unexpectedly failed! Please try again!"); }

        // apply the node settings
        Predecessor = response.Predecessor;
        FingerTable = new ChordFingerTable(
            response.FingerTable.ToDictionary(x => x.NodeId),
            (k, t) => LookupKey(k), config.MaxId);
        FingerTable.InsertFinger(Successor);
        Local.State = ChordHealthStatus.Idle;

        // -> the node is now ready for use

        // start the health monitoring / finger table update
        // procedures as scheduled background tasks
        createBackgroundTasks(backgroundTaskCallback.Token).Start();
    }

    private async Task createBackgroundTasks(CancellationToken token)
    {
        // run the 'monitor health' task on a regular time schedule
        var monitorTask = Task.Run(() => {

            while (!token.IsCancellationRequested)
            {
                monitorFingerHealth(token);
                Task.Delay(config.MonitorHealthSchedule * 1000).Wait();
            }
        });

        // run the 'update table' task on a regular time schedule
        var updateTableTask = Task.Run(() => {

            while (!token.IsCancellationRequested)
            {
                var timeout = new CancellationTokenSource();
                var updateTableTask = FingerTable.UpdateTable(NodeId, timeout.Token);
                var timeoutTask = Task.Delay(config.UpdateTableSchedule * 1000);
                if (Task.WaitAny(timeoutTask, updateTableTask) == 0)
                    timeout.Cancel();
            }
        });

        // wait until both tasks exited gracefully by cancellation
        await Task.WhenAll(new Task[] { monitorTask, updateTableTask });
    }

    public async Task LeaveNetwork()
    {
        // phase 1: initiate the leave process

        // send a leave initiation request to the successor
        var response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.InitNodeLeave,
                RequesterId = NodeId
            },
            Successor
        );

        if (!response.ReadyForDataCopy) {
            throw new InvalidOperationException("Network leave failed! Cannot copy payload data!"); }

        // phase 2: copy all existing payload data from this node to the successor

        // TODO: For production use, make sure to copy payload data, too.
        //       Keep in mind that this node is no more responsible for the ids
        //       being assigned to the successor. So there needs to be a kind of
        //       mechanism to copy the data first before safely leaving without
        //       temporary data unavailability.

        // phase 3: finalize the leave process

        // send a join initiation request to the successor
        // -> the successor sends an 'update successor' request to this node's predecessor
        // -> the predecessor's successor then points to this node's successor
        // -> from then on this node is no more available by other Chord nodes and can leave
        response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.CommitNodeLeave,
                RequesterId = NodeId,
                NewPredecessor = Predecessor
            },
            Successor
        );

        // make sure the successor did successfully commit the join
        if (!response.CommitSuccessful) { throw new InvalidOperationException(
            "Leaving the network unexpectedly failed! Please try again!"); }

        // shut down all background tasks (health monitoring and finger table updates)
        backgroundTaskCallback.Cancel();
    }

    public async Task<IChordEndpoint> LookupKey(
        ChordKey key, IChordEndpoint explicitReceiver=null)
    {
        // determine the receiver to be forwarded the lookup request to
        var receiver = explicitReceiver
            ?? FingerTable.FindBestFinger(key, Successor.NodeId);

        // send a key lookup request
        var response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.KeyLookup,
                RequesterId = NodeId,
                RequestedResourceId = key
            },
            receiver
        );

        // return the node responsible for the key
        return response.Responder;
    }

    public async Task<ChordHealthStatus> CheckHealth(
        IChordEndpoint target, int timeoutInSecs=10,
        ChordHealthStatus failStatus=ChordHealthStatus.Questionable)
    {
        // send a health check request
        var cancelCallback = new CancellationTokenSource();
        var timeoutTask = Task.Delay(timeoutInSecs * 1000);
        var healthCheckTask = Task.Run(() => sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.HealthCheck,
                RequesterId = NodeId,
            },
            target
        ), cancelCallback.Token);

        // return the reported health state or the fail status (timeout)
        bool timeout = await Task.WhenAny(timeoutTask, healthCheckTask) == timeoutTask;
        if (timeout) { cancelCallback.Cancel(); }
        return timeout ? failStatus : healthCheckTask.Result.Responder.State;
    }

    private void monitorFingerHealth(CancellationToken token)
    {
        const int healthCheckTimeout = 10; // TODO: parameterize the delay by configuration
        var cachedFingers = FingerTable.AllFingers.ToList();

        // perform a first health check for all finger nodes
        updateHealth(cachedFingers, healthCheckTimeout, ChordHealthStatus.Questionable);
        if (token.IsCancellationRequested) { return; } // TODO: release mutex

        // perform a second health check for all questionable finger nodes
        var questionableFingers = cachedFingers
            .Where(x => x.State == ChordHealthStatus.Questionable).ToList();
        updateHealth(questionableFingers, healthCheckTimeout / 2, ChordHealthStatus.Dead);
        if (token.IsCancellationRequested) { return; } // TODO: release mutex
    }

    private void updateHealth(List<IChordEndpoint> fingers, int timeoutSecs, 
        ChordHealthStatus failStatus=ChordHealthStatus.Questionable)
    {
        // run health checks for each finger in parallel
        var healthCheckTasks = FingerTable.AllFingers
            .Select(x => Task.Run(() => new { 
                NodeId = x.NodeId,
                Health=CheckHealth(x, timeoutSecs, failStatus).Result
            })).ToArray();
        Task.WaitAll(healthCheckTasks);

        // collect health check results
        var healthStates = healthCheckTasks.Select(x => x.Result)
            .ToDictionary(x => x.NodeId, x => x.Health);

        // update finger states
        fingers.ForEach(finger => finger.State = healthStates[finger.NodeId]);
    }
}
