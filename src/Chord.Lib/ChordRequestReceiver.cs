namespace Chord.Lib;

using ProcessRequestFunc = Func<IChordRequestMessage, Task<IChordResponseMessage>>;

public class ChordNodeRequestReceiver
{
    // TODO: synchronize the node state with an Actor-model event sourcing approach

    public ChordNodeRequestReceiver(
        IChordNode node,
        IChordClient sender)
    {
        this.node = node;
        this.sender = sender;

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
        // TODO: add a function to the ChordNode domain model for this use case

        // respond whether the update was successful
        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = canUpdate
        };
    }

    private async Task<IChordResponseMessage> processKeyLookup(IChordRequestMessage request)
    {
        // handle the special case for being the initial node of the cluster
        // seving the first lookup request of a node join
        if (node.Local.State == ChordHealthStatus.Starting)
        {
            // TODO: think of what else needs to be done here ...
            return new ChordResponseMessage() { Responder = node.Local };
        }

        // perform key lookup and return the endpoint responsible for the key
        var responder = await node.LookupKey(request.RequestedResourceId);

        // TODO: deconstruct LookupKey into a local cache lookup and a network request here ...
        //       -> get rid of the uncertainty that LookupKey might send network requests (!!!)

        return new ChordResponseMessage() { Responder = responder };
    }

    private async Task<IChordResponseMessage> processHealthCheck(IChordRequestMessage request)
        => await Task.FromResult(new ChordResponseMessage() { Responder = node.Local });

    private async Task<IChordResponseMessage> processInitNodeJoin(IChordRequestMessage request)
    {
        return await Task.FromResult(new ChordResponseMessage() {
            Responder = node.Local,
            ReadyForDataCopy = true
        });
    }

    private async Task<IChordResponseMessage> processCommitNodeJoin(IChordRequestMessage request)
    {
        var response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.UpdateSuccessor,
                RequesterId = node.NodeId,
                NewSuccessor = request.NewSuccessor
            },
            node.Predecessor);

        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = response.CommitSuccessful
        };
    }

    private async Task<IChordResponseMessage> processInitNodeLeave(IChordRequestMessage request)
    {
        return await Task.FromResult(new ChordResponseMessage() {
            Responder = node.Local,
            ReadyForDataCopy = true
        });
    }

    private async Task<IChordResponseMessage> processCommitNodeLeave(IChordRequestMessage request)
    {
        var response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.UpdateSuccessor,
                RequesterId = node.NodeId,
                NewSuccessor = request.NewSuccessor
            },
            node.Predecessor);

        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = response.CommitSuccessful
        };
    }
}
