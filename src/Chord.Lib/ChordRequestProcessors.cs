using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chord.Lib;

public interface IChordRequestProcessor
{
    Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request);
}

public class ChordNodeRequestProcessor : IChordRequestProcessor
{
    public ChordNodeRequestProcessor(
        IChordNode node,
        IChordRequestSender sender)
    {
        requestProcessors = new Dictionary<ChordRequestType, IChordRequestProcessor>() {
            { ChordRequestType.HealthCheck, new HealthCheckRequestProcessor(node) },
            { ChordRequestType.KeyLookup, new KeyLookupRequestProcessor(node, sender) },
            { ChordRequestType.UpdateSuccessor, new UpdateSuccessorRequestProcessor(node) },
            { ChordRequestType.InitNodeJoin, new InitNodeJoinRequestProcessor(node) },
            { ChordRequestType.CommitNodeJoin, new CommitNodeJoinRequestProcessor(node, sender) },
            { ChordRequestType.InitNodeLeave, new InitNodeLeaveRequestProcessor(node) },
            { ChordRequestType.CommitNodeLeave, new CommitNodeLeaveRequestProcessor(node, sender) },
        };
    }

    private readonly Dictionary<ChordRequestType, IChordRequestProcessor> requestProcessors;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
        => await requestProcessors[request.Type].ProcessAsync(request);
}

public class UpdateSuccessorRequestProcessor : IChordRequestProcessor
{
    public UpdateSuccessorRequestProcessor(IChordNode node)
        => this.node = node;

    private readonly IChordNode node;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
    {
        const int timeout = 10;

        // ping the new successor to make sure it is healthy
        var status = await node.CheckHealth(
            request.NewSuccessor, timeout, ChordHealthStatus.Dead);
        bool canUpdate = status != ChordHealthStatus.Dead;

        // update the successor
        if (canUpdate)
            node.UpdateSuccessor(request.NewSuccessor);
        // TODO: add a function to the ChordNode domain model for this use case

        // respond whether the update was successful
        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = canUpdate
        };
    }
}

public class KeyLookupRequestProcessor : IChordRequestProcessor
{
    public KeyLookupRequestProcessor(
        IChordNode node,
        IChordRequestSender sender)
    {
        this.node = node;
        this.sender = sender;
    }

    private readonly IChordNode node;
    private readonly IChordRequestSender sender;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
    {
        // handle the special case for being the initial node of the cluster
        // seving the first lookup request of a node join
        if (node.Local.State == ChordHealthStatus.Starting)
        {
            // TODO: think of what else needs to be done here ...
            return new ChordResponseMessage() { Responder = node.Local };
        }

        // perform key lookup and return the endpoint responsible for the key
        var responder = await node.LookupKey(request.RequestedResourceId);

        // TODO: deconstruct LookupKey into a local cache lookup and a network request here ...
        //       -> get rid of the uncertainty that LookupKey might send network requests (!!!)

        return new ChordResponseMessage() { Responder = responder };
    }
}

public class HealthCheckRequestProcessor : IChordRequestProcessor
{
    public HealthCheckRequestProcessor(IChordNode node)
        => this.node = node;

    private readonly IChordNode node;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
        => await Task.FromResult(new ChordResponseMessage() { Responder = node.Local });
}

public class InitNodeJoinRequestProcessor : IChordRequestProcessor
{
    public InitNodeJoinRequestProcessor(IChordNode node)
        => this.node = node;

    private readonly IChordNode node;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
    {
        // prepare for a joining node as new predecessor
        // inform the payload component that it has to send the payload
        // data chunk to the joining node that it is now responsible for

        // currently nothing to do here ...
        // TODO: trigger copy process for payload data transmission

        return await Task.FromResult(new ChordResponseMessage() {
            Responder = node.Local,
            ReadyForDataCopy = true
        });
    }
}

public class CommitNodeJoinRequestProcessor : IChordRequestProcessor
{
    public CommitNodeJoinRequestProcessor(
        IChordNode node,
        IChordRequestSender sender)
    {
        this.node = node;
        this.sender = sender;
    }

    private readonly IChordNode node;
    private readonly IChordRequestSender sender;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
    {
        // request the prodecessor's successor to be updated to the joining node
        // -> predecessor.successor = joining node
        // -> this.predecessor = joining node

        var response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.UpdateSuccessor,
                RequesterId = node.NodeId,
                NewSuccessor = request.NewSuccessor
            },
            node.Predecessor);

        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = response.CommitSuccessful
        };
    }
}

public class InitNodeLeaveRequestProcessor : IChordRequestProcessor
{
    public InitNodeLeaveRequestProcessor(IChordNode node)
    {
        this.node = node;
    }

    private readonly IChordNode node;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
    {
        // prepare for the predecessor leaving the network
        // inform the payload component that it will be sent payload data

        // currently nothing to do here ...

        return await Task.FromResult(new ChordResponseMessage() {
            Responder = node.Local,
            ReadyForDataCopy = true
        });
    }
}

public class CommitNodeLeaveRequestProcessor : IChordRequestProcessor
{
    public CommitNodeLeaveRequestProcessor(
        IChordNode node,
        IChordRequestSender sender)
    {
        this.node = node;
        this.sender = sender;
    }

    private readonly IChordNode node;
    private readonly IChordRequestSender sender;

    public async Task<IChordResponseMessage> ProcessAsync(IChordRequestMessage request)
    {
        var response = await sender.SendRequest(
            new ChordRequestMessage() {
                Type = ChordRequestType.UpdateSuccessor,
                RequesterId = node.NodeId,
                NewSuccessor = request.NewSuccessor
            },
            node.Predecessor);

        return new ChordResponseMessage() {
            Responder = node.Local,
            CommitSuccessful = response.CommitSuccessful
        };
    }
}