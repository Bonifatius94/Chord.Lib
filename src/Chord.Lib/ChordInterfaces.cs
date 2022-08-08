using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chord.Lib;

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

public interface IChordEndpoint
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
    IChordEndpoint NewSuccessor { get; set; }
    IChordEndpoint NewPredecessor { get; set; }
}

public interface IChordResponseMessage
{
    // core message features
    IChordEndpoint Responder { get; set; }

    // additional message features for the join/leave procedure
    bool ReadyForDataCopy { get; set; }
    bool CommitSuccessful { get; set; }
    IChordEndpoint Predecessor { get; set; }
    IEnumerable<IChordEndpoint> FingerTable { get; set; }
}

public interface IChordRequestSender
{
    Task<IChordResponseMessage> SendRequest(
        IChordRequestMessage request, IChordEndpoint receiver);
}

/// <summary>
/// Representing all functions and attributes of a logical chord node.
/// </summary>
public interface IChordNode
{
    /// <summary>
    /// The chord node's id.
    /// </summary>
    long NodeId { get; }

    /// <summary>
    /// The chord node's local endpoint.
    /// </summary>
    IChordEndpoint Local { get; }

    /// <summary>
    /// The chord node's successor node endpoint.
    /// </summary>
    IChordEndpoint Successor { get; }

    /// <summary>
    /// The chord node's predecessor node endpoint.
    /// </summary>
    IChordEndpoint Predecessor { get; }

    /// <summary>
    /// The chord node's finger table used for routing.
    /// </summary>
    IDictionary<long, IChordEndpoint> FingerTable { get; }

    /// <summary>
    /// Create a new chord endpoint and join it to the network.
    /// </summary>
    /// <param name="findBootstrapNode">A function searching the network for a bootstrap node.</param>
    /// <returns>a task handle to be awaited asynchronously</returns>
    Task JoinNetwork(Func<Task<IChordEndpoint>> findBootstrapNode);

    /// <summary>
    /// Shut down this chord endpoint by leaving the network gracefully.
    /// </summary>
    /// <returns>a task handle to be awaited asynchronously</returns>
    Task LeaveNetwork();

    /// <summary>
    /// Look up the chord node responsible for the given key.
    /// </summary>
    /// <param name="key">The key to be looked up.</param>
    /// <param name="explicitReceiver">An explicit receiver to send the request to (optional).</param>
    /// <returns>a task handle to be awaited asynchronously</returns>
    Task<IChordEndpoint> LookupKey(long key, IChordEndpoint explicitReceiver=null);

    /// <summary>
    /// Check the health status of the given target chord endpoint.
    /// </summary>
    /// <param name="target">The endpoint to check the health of.</param>
    /// <param name="timeoutInSecs">The timeout seconds to be waited for a response (default: 10s).</param>
    /// <param name="failStatus">The default status when the check times out (default: questionable).</param>
    /// <returns>a task handle to be awaited asynchronously</returns>
    Task<ChordHealthStatus> CheckHealth(
        IChordEndpoint target, int timeoutInSecs=10,
        ChordHealthStatus failStatus=ChordHealthStatus.Questionable);

    /// <summary>
    /// Process the given chord request the local endpoint just received.
    /// </summary>
    /// <param name="request">The chord request to be processed.</param>
    /// <returns>a task handle to be awaited asynchronously</returns>
    Task<IChordResponseMessage> ProcessRequest(IChordRequestMessage request);
}
