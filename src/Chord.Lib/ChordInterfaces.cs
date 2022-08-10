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
    ChordKey NodeId { get; }
    ChordHealthStatus State { get; }

    // TODO: remove IP endpoint implementation details from interface
    string IpAddress { get; }
    string Port { get; }

    IChordEndpoint DeepClone();
    ChordKey PickNewRandomId();
    void UpdateState(ChordHealthStatus newState);
}

public interface IChordRequestMessage
{
    // core message features
    ChordRequestType Type { get; set; }
    ChordKey RequesterId { get; set; }
    ChordKey RequestedResourceId { get; set; }

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
    IEnumerable<IChordEndpoint> CachedFingerTable { get; set; }
}

public interface IChordClient
{
    Task<IChordResponseMessage> SendRequest(
        IChordRequestMessage request,
        IChordEndpoint receiver,
        CancellationToken? token = null);
}

public interface IExplorableChordEndpointGenerator
{
    IEnumerable<IChordEndpoint> GenerateEndpoints();
}

public interface IChordBootstrapper
{
    Task<IChordEndpoint> FindBootstrapNode();
}

public interface IIpSettings
{
    /// <summary>
    /// Retrieve the chord node's IP address associated with the chord network's CIDR.
    /// (default: IP address from the first non-localhost notwork interface detected)
    /// </summary>
    /// <returns>the IP address specified in settings</returns>
    IPAddress ChordIpv4Address { get; }

    /// <summary>
    /// Retrieve the chord port from environment variable CHORD_PORT. (default: 9876)
    /// </summary>
    /// <returns>the network port specified in node settings as integer</returns>
    int ChordPort { get; }

    /// <summary>
    /// Retrieve the chord node's network ID.
    /// </summary>
    /// <returns>the IP address assiciated with the network ID</returns>
    IPAddress IPv4NetworkId { get; }

    /// <summary>
    /// Retrieve the chord node's broadcast address.
    /// </summary>
    /// <returns>the IP address assiciated with the broadcast address</returns>
    IPAddress IPv4Broadcast { get; }
}

/// <summary>
/// Representing all functions and attributes of a logical chord node.
/// </summary>
public interface IChordNode
{
    /// <summary>
    /// The chord node's id.
    /// </summary>
    ChordKey NodeId { get; }

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
    // ChordFingerTable FingerTable { get; }

    /// <summary>
    /// Create a new chord endpoint and join it to the network.
    /// </summary>
    /// <param name="local">The local Chord endpoint of the node.</param>
    /// <param name="bootstrapper">A bootstrap procedure provider.</param>
    /// <returns>a task handle to be awaited asynchronously</returns>
    Task JoinNetwork(IChordEndpoint local, IChordBootstrapper bootstrapper);

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
    Task<IChordEndpoint> LookupKey(ChordKey key, IChordEndpoint explicitReceiver=null);

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

    /// <summary>
    /// Notify the Chord node that it's successor changed.
    /// </summary>
    /// <param name="newSuccessor">The new successor of the node.</param>
    void UpdateSuccessor(IChordEndpoint newSuccessor);
}

public interface IChordPayloadWorker
{
    Task PreloadData(IChordEndpoint successor);
    Task BackupData(IChordEndpoint successor);
}
