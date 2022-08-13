namespace Chord.Lib;

using Microsoft.Extensions.Logging;
using ProcessRequestFunc = Func<IChordRequestMessage, Task<IChordResponseMessage>>;

public class ChordNodeRequestReceiver
{
    // TODO: synchronize the node state with an Actor-model event sourcing approach

    public ChordNodeRequestReceiver(
        IChordNode node,
        IChordClient sender,
        IChordPayloadWorker worker,
        ILogger logger = null)
    {
        this.node = node;
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

    private readonly IChordNode node;
    private readonly IChordClient sender;
    private readonly IChordPayloadWorker worker;
    private readonly ILogger logger;
    private readonly IDictionary<ChordRequestType, ProcessRequestFunc> handlers;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
        => await handlers[request.Type](request);

    private async Task<IChordResponseMessage> processUpdateSuccessor(IChordRequestMessage request)
    {
        const int timeout = 10;

        // ping the new successor to make sure it is healthy
        var status = await node.CheckHealth(
            request.NewSuccessor, timeout, ChordHealthStatus.Dead);
        bool canUpdate = status != ChordHealthStatus.Dead;

        // update the successor
        if (canUpdate)
            node.UpdateSuccessor(request.NewSuccessor);

        // respond whether the update was successful
        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = canUpdate
        };
    }

    private async Task<IChordResponseMessage> processKeyLookup(IChordRequestMessage request)
    {
        // handle the special case for being the initial node of the cluster
        // serving the first lookup request of a node join
        if (node.Local.State == ChordHealthStatus.Starting)
            return await Task.FromResult(new ChordResponseMessage() { Responder = node.Local });
            // TODO: think of what else needs to be done here ...

        // perform key lookup and return the endpoint responsible for the key
        var responder = await node.LookupKey(request.RequestedResourceId);
        return new ChordResponseMessage() { Responder = responder };
    }

    private async Task<IChordResponseMessage> processHealthCheck(IChordRequestMessage request)
        => await Task.FromResult(new ChordResponseMessage() { Responder = node.Local });

    private async Task<IChordResponseMessage> processInitNodeJoin(IChordRequestMessage request)
    {
        var task = worker.IsReadyForDataCopy();
        bool readyForDataCopy = await task.TryRun(
            (ex) => logger?.LogError(
                $"Payload worker data copy check failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = node.Local,
            ReadyForDataCopy = readyForDataCopy
        };
    }

    private async Task<IChordResponseMessage> processCommitNodeJoin(IChordRequestMessage request)
    {
        // TODO: move this inside the ChordRequestSender
        var task = sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.UpdateSuccessor,
                RequesterId = node.NodeId,
                NewSuccessor = request.NewSuccessor
            },
            node.Predecessor);

        bool commitSuccessful = await task.TryRun(
            (r) => r.CommitSuccessful,
            (ex) => logger?.LogError(
                $"Updating the successor of {node.Predecessor.NodeId} failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = node.Local,
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
            Responder = node.Local,
            ReadyForDataCopy = readyForDataCopy
        };
    }

    private async Task<IChordResponseMessage> processCommitNodeLeave(IChordRequestMessage request)
    {
        var task = sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.UpdateSuccessor,
                RequesterId = node.NodeId,
                NewSuccessor = request.NewSuccessor
            },
            node.Predecessor);

        bool commitSuccessful = await task.TryRun(
            (r) => r.CommitSuccessful,
            (ex) => logger?.LogError(
                $"Updating the successor of {node.Predecessor.NodeId} failed!\nException:{ex}"),
            false);

        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = commitSuccessful
        };
    }
}
