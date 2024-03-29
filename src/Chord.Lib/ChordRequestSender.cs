namespace Chord.Lib;

public class ChordRequestSender
{
    // TODO: synchronize the node state with an event sourcing approach

    public ChordRequestSender(
        IChordRequestProcessor client,
        IChordNetworkRouter router)
    {
        this.client = client;
        this.router = router;
    }

    private readonly IChordRequestProcessor client;
    private readonly IChordNetworkRouter router;

    // TODO: add fault tolerance with TryRun()
    // TODO: make each function cancelable by token argument

    public async Task<IChordEndpoint> IntiateSuccessor(
        IChordEndpoint bootstrapNode,
        IChordEndpoint local,
        CancellationToken token)
    {
        // TODO: think about stopping to try after a timeout

        if (bootstrapNode.NodeId == local.NodeId)
            return local;

        while (true)
        {
            var successor = await SearchEndpointOfKey(
                local.NodeId, local, token, bootstrapNode);

            // found a successor and the local endpoint's id is unique
            if (successor != null && local.NodeId != successor.NodeId)
                return successor;

            local.PickNewRandomId();
        }
    }

    public async Task<IChordResponseMessage> InitiateNetworkJoin(
        IChordEndpoint local,
        IChordEndpoint successor,
        CancellationToken token)
    {
        var request = new ChordRequestMessage() {
            Receiver = successor,
            Type = ChordRequestType.InitNodeJoin,
            RequesterId = local.NodeId
        };
        return await client.ProcessRequest(request, token);
    }

    public async Task<IChordResponseMessage> CommitNetworkJoin(
        IChordEndpoint local,
        IChordEndpoint successor,
        CancellationToken token)
    {
        var request = new ChordRequestMessage() {
            Receiver = successor,
            Type = ChordRequestType.CommitNodeJoin,
            RequesterId = local.NodeId,
            NewSuccessor = local
        };
        return await client.ProcessRequest(request, token);
    }

    public async Task<IChordResponseMessage> InitiateNetworkLeave(
        IChordEndpoint local,
        IChordEndpoint successor,
        CancellationToken token)
    {
        var request = new ChordRequestMessage() {
            Receiver = successor,
            Type = ChordRequestType.InitNodeLeave,
            RequesterId = local.NodeId
        };
        return await client.ProcessRequest(request, token);
    }

    public async Task<IChordResponseMessage> CommitNetworkLeave(
        IChordEndpoint local,
        IChordEndpoint successor,
        IChordEndpoint predecessor,
        CancellationToken token)
    {
        var request = new ChordRequestMessage() {
            Receiver = successor,
            Type = ChordRequestType.CommitNodeLeave,
            RequesterId = local.NodeId,
            NewPredecessor = predecessor
        };
        return await client.ProcessRequest(request, token);
    }

    public async Task<IChordEndpoint> SearchEndpointOfKey(
        ChordKey key,
        IChordEndpoint local,
        CancellationToken token,
        IChordEndpoint explicitReceiver = null)
    {
        var request = new ChordRequestMessage() {
            Receiver = explicitReceiver ?? router.FindBestFinger(key),
            Type = ChordRequestType.KeyLookup,
            RequesterId = local.NodeId,
            RequestedResourceId = key
        };
        return await client
            .ProcessRequest(request, token)
            .TryRun((r) => r.Responder, (ex) => {}, null);
    }

    public async Task<ChordHealthStatus> HealthCheck(
        IChordEndpoint local,
        IChordEndpoint receiver,
        CancellationToken token,
        ChordHealthStatus failStatus = ChordHealthStatus.Questionable,
        int timeoutInMillis = 10000)
    {
        var request = new ChordRequestMessage() {
            Receiver = receiver,
            Type = ChordRequestType.HealthCheck,
            RequesterId = local.NodeId,
        };
        return await client
            .ProcessRequest(request, token)
            .TryRun((r) => r.Responder.State, (ex) => {}, failStatus)
            // TODO: do the timeout within the IChordClient part, don't bother here ...
            .Timeout(timeoutInMillis, failStatus, token);
    }

    public async Task<IChordResponseMessage> UpdateSuccessor(
        IChordEndpoint local,
        IChordEndpoint predecessor,
        IChordEndpoint newSuccessor,
        CancellationToken token)
    {
        var request = new ChordRequestMessage() {
            Receiver = predecessor,
            Type = ChordRequestType.UpdateSuccessor,
            RequesterId = local.NodeId,
            NewSuccessor = newSuccessor
        };
        return await client.ProcessRequest(request, token);
    }
}
