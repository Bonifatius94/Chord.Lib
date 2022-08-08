namespace Chord.Lib;

public class ChordEndpoint : IChordEndpoint
{
    // summary of remote node features
    public ChordKey NodeId { get; set; }
    public string IpAddress { get; set; }
    public string Port { get; set; }
    public ChordHealthStatus State { get; set; } = ChordHealthStatus.Questionable;

    // TODO: pull some of the domain logic in here as well ...
}
