using System.Collections.Generic;

namespace Chord.Lib.Core
{
    public enum ChordRequestType
    {
        KeyLookup,
        UpdateSuccessor,
        HealthCheck,
        InitNodeJoin,
        CommitNodeJoin,
        InitNodeLeave,
        CommitNodeLeave,
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
        ChordHealthStatus State { get; set; }
    }

    public interface IChordRequestMessage
    {
        // core message features
        ChordRequestType Type { get; set; }
        long RequesterId { get; set; }
        long RequestedResourceId { get; set; }

        // additional message features for the join/leave procedure
        long NewSuccessor { get; set; }
    }

    public interface IChordResponseMessage
    {
        // core message features
        IChordRemoteNode Responder { get; set; }

        // additional message features for the join/leave procedure
        bool ReadyForDataCopy { get; set; }
        IChordRemoteNode Predecessor { get; set; }
        IEnumerable<IChordRemoteNode> FingerTable { get; set; }
    }

    public class ChordRemoteNode
    {
        // summary of remote node features
        public long NodeId { get; set; }
        public string IpAddress { get; set; }
        public string Port { get; set; }
        public ChordHealthStatus State { get; set; } = ChordHealthStatus.Questionable;
    }

    public class ChordRequestMessage : IChordRequestMessage
    {
        // core request message features
        public ChordRequestType Type { get; set; }
        public long RequesterId { get; set; }
        public long RequestedResourceId { get; set; }

        // additional message features for the join/leave procedure
        public long NewSuccessor { get; set; }
    }

    public class ChordResponseMessage : IChordResponseMessage
    {
        // core response message features
        public IChordRemoteNode Responder { get; set; }

        // additional message features for the join/leave procedure
        public bool ReadyForDataCopy { get; set; }
        public IChordRemoteNode Predecessor { get; set; }
        public IEnumerable<IChordRemoteNode> FingerTable { get; set; }
    }
}
