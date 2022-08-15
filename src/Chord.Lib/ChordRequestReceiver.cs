namespace Chord.Lib;

using Microsoft.Extensions.Logging;
using ProcessRequestFunc = Func<IChordRequestMessage, Task<IChordResponseMessage>>;

public class ChordRequestReceiver
{
    // TODO: synchronize the node state with an Actor-model event sourcing approach

    #region Init

    public ChordRequestReceiver(
        Func<IChordNodeState> getNodeState,
        ChordRequestSender sender,
        IChordPayloadWorker worker,
        ILogger logger = null)
    {
        this.getNodeState = getNodeState;
        this.sender = sender;
        this.worker = worker;
        this.logger = logger;

        handlers = new Dictionary<ChordRequestType, ProcessRequestFunc>() {
            { ChordRequestType.HealthCheck, processHealthCheck },
            { ChordRequestType.KeyLookup, processKeyLookup },
            { ChordRequestType.UpdateSuccessor, processUpdateSuccessor },
            { ChordRequestType.InitNodeJoin, processInitNodeJoin },
            { ChordRequestType.CommitNodeJoin, processCommitNodeJoin },
            { ChordRequestType.InitNodeLeave, processInitNodeLeave },
            { ChordRequestType.CommitNodeLeave, processCommitNodeLeave },
        };
    }

    private readonly Func<IChordNodeState> getNodeState; // TODO: get rid of this, don't re-create the finger table in ChordNode
    private IChordNodeState nodeState => getNodeState();

    private readonly ChordRequestSender sender;
    private readonly IChordPayloadWorker worker;
    private readonly ILogger logger;
    private readonly IDictionary<ChordRequestType, ProcessRequestFunc> handlers;

    #endregion Init

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
        => await handlers[request.Type](request);

    private async Task<IChordResponseMessage> processUpdateSuccessor(IChordRequestMessage request)
    {
        const int timeoutInMillis = 10 * 1000;

        // ping the new successor to make sure it is healthy
        var status = await sender.HealthCheck(
            nodeState.Local, request.NewSuccessor, ChordHealthStatus.Dead, timeoutInMillis);
        bool canUpdate = status != ChordHealthStatus.Dead;

        // update the successor
        if (canUpdate)
            nodeState.UpdateSuccessor(request.NewSuccessor);

        // respond whether the update was successful
        return new ChordResponseMessage() {
            Responder = nodeState.Local,
            CommitSuccessful = canUpdate
        };
    }

    private async Task<IChordResponseMessage> processKeyLookup(IChordRequestMessage request)
    {
        // handle the special case for being the initial node of the cluster
        // serving the first lookup request of a node join
        if (nodeState.Local.State == ChordHealthStatus.Starting)
            return await Task.FromResult(new ChordResponseMessage() { Responder = nodeState.Local });
            // TODO: think of what else needs to be done here ...

        // perform key lookup and return the endpoint responsible for the key
        var responder = await sender.SearchEndpointOfKey(request.RequestedResourceId, nodeState.Local);
        return new ChordResponseMessage() { Responder = responder };
    }

    private async Task<IChordResponseMessage> processHealthCheck(IChordRequestMessage request)
        => await Task.FromResult(new ChordResponseMessage() { Responder = nodeState.Local });

    private async Task<IChordResponseMessage> processInitNodeJoin(IChordRequestMessage request)
    {
        var task = worker.IsReadyForDataCopy();
        bool readyForDataCopy = await task.TryRun(
            (ex) => logger?.LogError(
                $"Payload worker data copy check failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = nodeState.Local,
            ReadyForDataCopy = readyForDataCopy
        };
    }

    private async Task<IChordResponseMessage> processCommitNodeJoin(IChordRequestMessage request)
    {
        var task = sender.UpdateSuccessor(nodeState.Local, nodeState.Predecessor, request.NewSuccessor);

        bool commitSuccessful = await task.TryRun(
            (r) => r.CommitSuccessful,
            (ex) => logger?.LogError(
                $"Updating the successor of {nodeState.Predecessor.NodeId} failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = nodeState.Local,
            CommitSuccessful = commitSuccessful
        };
    }

    private async Task<IChordResponseMessage> processInitNodeLeave(IChordRequestMessage request)
    {
        var task = worker.IsReadyForDataCopy();
        bool readyForDataCopy = await task.TryRun(
            (ex) => logger?.LogError(
                $"Payload worker data copy check failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = nodeState.Local,
            ReadyForDataCopy = readyForDataCopy
        };
    }

    private async Task<IChordResponseMessage> processCommitNodeLeave(IChordRequestMessage request)
    {
        var task = sender.UpdateSuccessor(nodeState.Local, nodeState.Predecessor, request.NewSuccessor);

        bool commitSuccessful = await task.TryRun(
            (r) => r.CommitSuccessful,
            (ex) => logger?.LogError(
                $"Updating the successor of {nodeState.Predecessor.NodeId} failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = nodeState.Local,
            CommitSuccessful = commitSuccessful
        };
    }
}
