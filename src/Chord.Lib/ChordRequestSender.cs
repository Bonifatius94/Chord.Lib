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
        IChordEndpoint local)
    {
        // TODO: think about stopping to try after a timeout

        while (true)
        { 
            var successor = await SearchEndpointOfKey(
                local.NodeId, local, bootstrapNode);

            // found a successor and the local endpoint's id is unique
            if (successor != null && local.NodeId != successor.NodeId)
                return successor;

            local.PickNewRandomId();
        }
    }

    public async Task<IChordResponseMessage> InitiateNetworkJoin(
            IChordEndpoint local,
            IChordEndpoint successor)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.InitNodeJoin,
                    RequesterId = local.NodeId
                },
                successor
            );

    public async Task<IChordResponseMessage> CommitNetworkJoin(
            IChordEndpoint local,
            IChordEndpoint successor)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.CommitNodeJoin,
                    RequesterId = local.NodeId,
                    NewSuccessor = local
                },
                successor
            );

    public async Task<IChordResponseMessage> InitiateNetworkLeave(
            IChordEndpoint local,
            IChordEndpoint successor)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.InitNodeLeave,
                    RequesterId = local.NodeId
                },
                successor
            );

    public async Task<IChordResponseMessage> CommitNetworkLeave(
            IChordEndpoint local,
            IChordEndpoint successor,
            IChordEndpoint predecessor)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.CommitNodeLeave,
                    RequesterId = local.NodeId,
                    NewPredecessor = predecessor
                },
                successor
            );

    public async Task<IChordEndpoint> SearchEndpointOfKey(
            ChordKey key,
            IChordEndpoint local,
            IChordEndpoint explicitReceiver = null)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.KeyLookup,
                    RequesterId = local.NodeId,
                    RequestedResourceId = key
                },
                explicitReceiver ?? router.FindBestFinger(key))
            .TryRun(
                (r) => r.Responder,
                (ex) => {},
                null);

    public async Task<ChordHealthStatus> HealthCheck(
            IChordEndpoint local,
            IChordEndpoint receiver,
            ChordHealthStatus failStatus = ChordHealthStatus.Questionable,
            int timeoutInMillis = 10000,
            CancellationToken? token = null)
        => await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.HealthCheck,
                    RequesterId = local.NodeId,
                },
                receiver)
            .TryRun(
                (r) => r.Responder.State,
                (ex) => {},
                failStatus)
            // TODO: do the timeout within the IChordClient part, don't bother here ...
            .Timeout(
                timeoutInMillis,
                failStatus);

    public async Task<IChordResponseMessage> UpdateSuccessor(
            IChordEndpoint local,
            IChordEndpoint predecessor,
            IChordEndpoint newSuccessor)
    {
        // base case for creating a Chord ring with only 2 nodes
        if (predecessor == null)
            return await Task.FromResult(new ChordResponseMessage() {
                Responder = local,
                CommitSuccessful = true,
                Predecessor = local
            });

        return await client
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.UpdateSuccessor,
                    RequesterId = local.NodeId,
                    NewSuccessor = newSuccessor
                },
                predecessor);
    }
}
