namespace Chord.Lib;

public class ChordEndpoint : IChordEndpoint
{
    // summary of remote node features
    public long NodeId { get; set; }
    public string IpAddress { get; set; }
    public string Port { get; set; }
    public ChordHealthStatus State { get; set; } = ChordHealthStatus.Questionable;
}
