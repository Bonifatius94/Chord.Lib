namespace Chord.Lib;

using Microsoft.Extensions.Logging;
using ProcessRequestFunc = Func<
    IChordRequestMessage,
    CancellationToken,
    Task<IChordResponseMessage>>;

public class ChordRequestReceiver : IChordRequestProcessor
{
    // TODO: synchronize the node state with an Actor-model event sourcing approach

    #region Init

    public ChordRequestReceiver(
        ChordNodeState nodeState,
        ChordRequestSender sender,
        IChordPayloadWorker worker,
        ILogger logger = null)
    {
        this.nodeState = nodeState;
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

    private readonly ChordNodeState nodeState;
    private readonly ChordRequestSender sender;
    private readonly IChordPayloadWorker worker;
    private readonly ILogger logger;
    private readonly IDictionary<ChordRequestType, ProcessRequestFunc> handlers;

    #endregion Init

    public async Task<IChordResponseMessage> ProcessRequest(
            IChordRequestMessage request, CancellationToken token)
        => await handlers[request.Type](request, token);

    private async Task<IChordResponseMessage> processUpdateSuccessor(
        IChordRequestMessage request,
        CancellationToken token)
    {
        const int timeoutInMillis = 10 * 1000;

        // ping the new successor to make sure it is healthy
        var status = await sender.HealthCheck(
            nodeState.Local, request.NewSuccessor,
            token, ChordHealthStatus.Dead, timeoutInMillis);
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

    private async Task<IChordResponseMessage> processKeyLookup(
        IChordRequestMessage request,
        CancellationToken token)
    {
        // handle the special case for being the initial node of the cluster
        // serving the first lookup request of a node join
        if (nodeState.Local.State == ChordHealthStatus.Starting)
            return await Task.FromResult(new ChordResponseMessage() { Responder = nodeState.Local });
            // TODO: think of what else needs to be done here ...

        if (nodeState.FingerCount == 1)
            return new ChordResponseMessage() { Responder = nodeState.Local };

        // perform key lookup and return the endpoint responsible for the key
        var responder = await sender.SearchEndpointOfKey(
            request.RequestedResourceId, nodeState.Local, token);
        return new ChordResponseMessage() { Responder = responder };
    }

    private async Task<IChordResponseMessage> processHealthCheck(
            IChordRequestMessage request,
            CancellationToken token)
        => await Task.FromResult(new ChordResponseMessage() { Responder = nodeState.Local });

    private async Task<IChordResponseMessage> processInitNodeJoin(
        IChordRequestMessage request,
        CancellationToken token)
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

    private async Task<IChordResponseMessage> processCommitNodeJoin(
        IChordRequestMessage request,
        CancellationToken token)
    {
        // base case for creating a Chord ring with only 2 nodes
        if (nodeState.Predecessor == null)
        {
            nodeState.UpdatePredecessor(request.NewSuccessor);
            nodeState.UpdateSuccessor(request.NewSuccessor);
            return await Task.FromResult(new ChordResponseMessage() {
                Responder = nodeState.Local,
                CommitSuccessful = true,
                Predecessor = nodeState.Local,
                CachedFingerTable = new List<IChordEndpoint>() {
                    nodeState.Local,
                    request.NewSuccessor
                }
            });
        }

        var task = sender.UpdateSuccessor(
            nodeState.Local, nodeState.Predecessor, request.NewSuccessor, token);

        bool commitSuccessful = await task.TryRun(
            (r) => r.CommitSuccessful,
            (ex) => logger?.LogError(
                $"Updating the successor of {nodeState.Predecessor?.NodeId} failed!\nException:{ex}"),
            false);

        if (commitSuccessful)
            nodeState.UpdatePredecessor(request.NewSuccessor);

        return new ChordResponseMessage() {
            Responder = nodeState.Local,
            CommitSuccessful = commitSuccessful,
            Predecessor = nodeState.Predecessor
        };
    }

    private async Task<IChordResponseMessage> processInitNodeLeave(
        IChordRequestMessage request,
        CancellationToken token)
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

    private async Task<IChordResponseMessage> processCommitNodeLeave(
        IChordRequestMessage request,
        CancellationToken token)
    {
        var task = sender.UpdateSuccessor(
            nodeState.Local, nodeState.Predecessor, request.NewSuccessor, token);

        bool commitSuccessful = await task.TryRun(
            (r) => r.CommitSuccessful,
            (ex) => logger?.LogError(
                $"Updating the successor of {nodeState.Predecessor?.NodeId} failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = nodeState.Local,
            CommitSuccessful = commitSuccessful
        };
    }
}
