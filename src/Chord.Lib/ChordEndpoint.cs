namespace Chord.Lib;

public class ChordEndpoint : IChordEndpoint
{
    public ChordEndpoint(
        string ipAddress,
        string port,
        BigInteger keySpace,
        ChordHealthStatus state = ChordHealthStatus.Starting)
    {
        NodeId = ChordKey.PickRandom(keySpace);
        State = state;
        IpAddress = ipAddress;
        Port = port;
    }

    public ChordEndpoint(
        ChordKey nodeId,
        ChordHealthStatus state,
        string ipAddress,
        string port)
    {
        NodeId = nodeId;
        State = state;
        IpAddress = ipAddress;
        Port = port;
    }

    // summary of remote node features
    public ChordKey NodeId { get; private set; }
    public ChordHealthStatus State { get; private set; } = ChordHealthStatus.Questionable;
    public string IpAddress { get; private set; }
    public string Port { get; private set; }

    public IChordEndpoint DeepClone()
        => new ChordEndpoint(NodeId, State, IpAddress, Port);

    public ChordKey PickNewRandomId()
        => NodeId = ChordKey.PickRandom(NodeId.KeySpace);

    public void UpdateState(ChordHealthStatus newState)
        => State = newState;

    public override string ToString() => $"{NodeId}";

    // TODO: pull some of the domain logic in here as well ...
}
