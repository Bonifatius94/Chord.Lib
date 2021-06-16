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
        long NodeId { get; set; }
        string IpAddress { get; set; }
        string Port { get; set; }
    }

    public interface IChordRequestMessage
    {
        // core message features
        ChordRequestType Type { get; set; }
        long RequesterId { get; set; }
        long RequestedResourceId { get; set; }

        // TODO: make sure those features suffice
    }

    public interface IChordResponseMessage
    {
        // core message features
        IChordRemoteNode Responder { get; set; }

        // additional message features for the join procedure
        IChordRemoteNode Predecessor { get; set; }
        IEnumerable<IChordRemoteNode> FingerTable { get; set; }
    }

    public class ChordRemoteNode
    {
        // summary of remote node features
        public long NodeId { get; set; }
        public string IpAddress { get; set; }
        public string Port { get; set; }
    }

    public class ChordRequestMessage : IChordRequestMessage
    {
        public ChordRequestType Type { get; set; }
        public long RequesterId { get; set; }
        public long RequestedResourceId { get; set; }
    }

    public class ChordResponseMessage : IChordResponseMessage
    {
        // core message features
        public IChordRemoteNode Responder { get; set; }

        // additional message features for the join procedure
        public IChordRemoteNode Predecessor { get; set; }
        public IEnumerable<IChordRemoteNode> FingerTable { get; set; }
    }
}
