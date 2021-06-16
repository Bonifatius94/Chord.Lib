using System.Collections.Generic;

namespace Chord.Lib.Core
{
    public enum ChordRequestType
    {
        KeyLookup,
        UpdateSuccessor,
        HealthCheck,
        InitNodeJoin,
        InitNodeLeave,
    }

    public enum ChordHealthStatus
    {
        Starting,
        Idle,
        Leaving,
        Questionable,
        Dead
    }

    public interface IChordRemoteNode
    {
        // summary of remote node features
        string NodeId { get; set; }
        string IpAddress { get; set; }
        string Port { get; set; }
    }

    public interface IChordRequestMessage
    {
        // core message features
        ChordRequestType Type { get; set; }
        string RequesterId { get; set; }
        string RequestedResourceId { get; set; }

        // TODO: make sure those features suffice
    }

    public interface IChordResponseMessage
    {
        // core message features
        ChordRequestType Type { get; set; }
        string RequesterId { get; set; }
        string RequestedResourceId { get; set; }
        string ResponderId { get; set; }

        // additional message features for the join procedure
        string PredecessorId { get; set; }
        IEnumerable<IChordRemoteNode> FingerTable { get; set; }
    }
}
