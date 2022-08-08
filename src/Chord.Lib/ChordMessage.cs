using System.Collections.Generic;

namespace Chord.Lib;

public class ChordRequestMessage : IChordRequestMessage
{
    // core request message features
    public ChordRequestType Type { get; set; }
    public ChordKey RequesterId { get; set; }
    public ChordKey RequestedResourceId { get; set; }

    // additional message features for the join/leave procedure
    public IChordEndpoint NewSuccessor { get; set; }
    public IChordEndpoint NewPredecessor { get; set; }
}

public class ChordResponseMessage : IChordResponseMessage
{
    // core response message features
    public IChordEndpoint Responder { get; set; }

    // additional message features for the join/leave procedure
    public bool ReadyForDataCopy { get; set; }
    public bool CommitSuccessful { get; set; }
    public IChordEndpoint Predecessor { get; set; }
    public IEnumerable<IChordEndpoint> FingerTable { get; set; }
}
