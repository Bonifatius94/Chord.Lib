namespace Chord.Lib;

public class ChordRequestSender
{
    // TODO: synchronize the node state with an event sourcing approach

    public ChordRequestSender(
        IChordClient client,
        IChordNetworkRouter router)
    {
        this.client = client;
        this.router = router;
    }

    private readonly IChordClient client;
    private readonly IChordNetworkRouter router;

    // TODO: add fault tolerance with TryRun()
    // TODO: make each function cancelable by token argument

    public async Task<IChordEndpoint> IntiateSuccessor(
        IChordEndpoint bootstrapNode,
        IChordEndpoint local,
        CancellationToken token)
    {
        // TODO: think about stopping to try after a timeout

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
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.InitNodeJoin,
                    RequesterId = local.NodeId
                },
                successor,
                token);

    public async Task<IChordResponseMessage> CommitNetworkJoin(
            IChordEndpoint local,
            IChordEndpoint successor,
            CancellationToken token)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.CommitNodeJoin,
                    RequesterId = local.NodeId,
                    NewSuccessor = local
                },
                successor,
                token);

    public async Task<IChordResponseMessage> InitiateNetworkLeave(
            IChordEndpoint local,
            IChordEndpoint successor,
            CancellationToken token)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.InitNodeLeave,
                    RequesterId = local.NodeId
                },
                successor,
                token);

    public async Task<IChordResponseMessage> CommitNetworkLeave(
            IChordEndpoint local,
            IChordEndpoint successor,
            IChordEndpoint predecessor,
            CancellationToken token)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.CommitNodeLeave,
                    RequesterId = local.NodeId,
                    NewPredecessor = predecessor
                },
                successor,
                token);

    public async Task<IChordEndpoint> SearchEndpointOfKey(
            ChordKey key,
            IChordEndpoint local,
            CancellationToken token,
            IChordEndpoint explicitReceiver = null)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.KeyLookup,
                    RequesterId = local.NodeId,
                    RequestedResourceId = key
                },
                explicitReceiver ?? router.FindBestFinger(key),
                token)
            .TryRun(
                (r) => r.Responder,
                (ex) => {},
                null);

    public async Task<ChordHealthStatus> HealthCheck(
            IChordEndpoint local,
            IChordEndpoint receiver,
            CancellationToken token,
            ChordHealthStatus failStatus = ChordHealthStatus.Questionable,
            int timeoutInMillis = 10000)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.HealthCheck,
                    RequesterId = local.NodeId,
                },
                receiver,
                token)
            .TryRun(
                (r) => r.Responder.State,
                (ex) => {},
                failStatus)
            // TODO: do the timeout within the IChordClient part, don't bother here ...
            .Timeout(
                timeoutInMillis,
                failStatus,
                token);

    public async Task<IChordResponseMessage> UpdateSuccessor(
            IChordEndpoint local,
            IChordEndpoint predecessor,
            IChordEndpoint newSuccessor,
            CancellationToken token)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.UpdateSuccessor,
                    RequesterId = local.NodeId,
                    NewSuccessor = newSuccessor
                },
                predecessor,
                token);
}
