namespace Chord.Lib;

public class ChordRequestSender
{
    // TODO: synchronize the node state with an Actor-model event sourcing approach

    public ChordRequestSender(IChordClient client)
        => this.client = client;

    private readonly IChordClient client;

    public async Task<IChordEndpoint> IntiateSuccessor(
        IChordEndpoint bootstrapNode,
        IChordEndpoint local)
    {
        IChordEndpoint successor;

        do {
            local.PickNewRandomId();
            var response = await SearchEndpointOfKey(local.NodeId, local, bootstrapNode);
            successor = response.Responder;
        } while (successor.NodeId == local.NodeId);

        return successor;
    }

    public async Task<IChordResponseMessage> InitiateNetworkJoin(
            IChordEndpoint local, IChordEndpoint successor)
        => await client.SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.InitNodeJoin,
                    RequesterId = local.NodeId
                },
                successor
            );

    public async Task<IChordResponseMessage> CommitNetworkJoin(
            IChordEndpoint local, IChordEndpoint successor)
        => await client.SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.CommitNodeJoin,
                    RequesterId = local.NodeId,
                    NewSuccessor = local
                },
                successor
            );

    public async Task<IChordResponseMessage> InitiateNetworkLeave(
            IChordEndpoint local, IChordEndpoint successor)
        => await client.SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.InitNodeLeave,
                    RequesterId = local.NodeId
                },
                successor
            );

    public async Task<IChordResponseMessage> CommitNetworkLeave(
            IChordEndpoint local, IChordEndpoint successor, IChordEndpoint predecessor)
        => await client.SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.CommitNodeLeave,
                    RequesterId = local.NodeId,
                    NewPredecessor = predecessor
                },
                successor
            );

    public async Task<IChordResponseMessage> SearchEndpointOfKey(
            ChordKey key, IChordEndpoint local, IChordEndpoint receiver)
        => await client.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.KeyLookup,
                RequesterId = local.NodeId,
                RequestedResourceId = key
            },
            receiver
        );

    public async Task<IChordResponseMessage> HealthCheck(
            IChordEndpoint local, IChordEndpoint receiver, CancellationToken token)
        => await client.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.HealthCheck,
                RequesterId = local.NodeId,
            },
            receiver,
            token);
}
