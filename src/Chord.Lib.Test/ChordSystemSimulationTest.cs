using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib.Impl;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Chord.Lib.Test;

class LoggerAdapter : ILogger
{
    public LoggerAdapter(ITestOutputHelper logger)
        => this.logger = logger;

    private ITestOutputHelper logger;

    // info: this is just a dummy to make the interface work
    public IDisposable BeginScope<TState>(TState state)
        => new StreamWriter(new MemoryStream());

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        => logger.WriteLine($"{DateTime.UtcNow} {logLevel}: {formatter(state, exception)}");
}

class IPv4NetworkMock : IChordRequestProcessor
{
    // info: this is a list instead of a dict on purpose as
    //       the NodeId might change during the node init
    public List<ChordNode> Nodes { get; set; }
        = new List<ChordNode>();

    private HashSet<ChordRequestType> interceptTypes =
        new HashSet<ChordRequestType>() {
            ChordRequestType.KeyLookup,
            ChordRequestType.UpdateSuccessor,
            ChordRequestType.InitNodeJoin,
            ChordRequestType.CommitNodeJoin
        };

    public async Task<IChordResponseMessage> ProcessRequest(
        IChordRequestMessage request,
        CancellationToken token)
    {
        // look up the node that's receiving the request
        var receiver = request.Receiver;
        var receivingNode = Nodes
            .Where(x => x.NodeId == receiver.NodeId)
            .FirstOrDefault();

        // simulate a network timeout because the node doesn't exist
        if (receivingNode == null)
        {
            await Task.Delay(1000);
            throw new HttpRequestException(
                $"Endpoint with id {receiver.NodeId} not found!");
        }

        // TODO: think of adding random failures

        // simulate a short delay, then process the request
        await Task.Delay(5);
        return await receivingNode.ProcessRequest(request, token);
    }
}

public class ChordNetworkSimulationTest
{
    private readonly ITestOutputHelper _logger;
    public ChordNetworkSimulationTest(ITestOutputHelper logger)
    {
        _logger = logger;
    }

    [Fact(Skip="code is not ready yet")]
    public async Task SimulateNetwork()
    {
        // define test hyperparams
        const int testNodesCount = 100;
        const int keySpace = 100000;
        const int chordPort = 9876;

        var endpoints = Enumerable.Range(1, testNodesCount)
            .Select(x => new IPv4Endpoint(
                ChordKey.PickRandom(keySpace),
                ChordHealthStatus.Starting,
                $"10.0.0.{ x }",
                chordPort.ToString()))
            .ToList();

        var networkClient = new IPv4NetworkMock();
        var nodes = endpoints
            .Select(local =>
                new ChordNode(
                    local,
                    networkClient,
                    new ZeroProtocolPayloadWorker(),
                    new ChordNodeConfiguration(),
                    new LoggerAdapter(_logger)))
            .ToList();
        networkClient.Nodes = nodes;

        // init network simulation stuff
        var bootstrapNode = endpoints.First<IChordEndpoint>();
        var bootstrapperMock = Substitute.For<IChordBootstrapper>();
        bootstrapperMock.FindBootstrapNode(default, default)
            .ReturnsForAnyArgs(x => Task.FromResult(
                endpoints.Except(new List<IChordEndpoint> {
                    x[1] as IChordEndpoint }).First()));

        var retryTimeouts = new int[] { 25, 50, 100, 200, 400, 800, 1600 };
        var exHandler = (Exception ex) => _logger.WriteLine(ex.ToString());
        var cancelCallback = new CancellationTokenSource();
        var joinTasks = nodes.Select(node => {
            var joinTaskFactory = () => node.JoinNetwork(
                bootstrapperMock, cancelCallback.Token);
            return joinTaskFactory.TryRepeat(retryTimeouts, exHandler);
        });

        await Task.WhenAll(joinTasks);
        nodes.Should().Match(x => x.All(y => y.NodeState == ChordHealthStatus.Idle));
        nodes.Should().Match(x => x.All(y => y.Successor != null));
        nodes.Should().Match(x => x.All(y => y.Predecessor != null));

        int i = 0;
        var network = chordNetworkStructure(nodes);
        foreach (var ring in network)
        {
            var nodeDescriptions = ring.Select(x =>
                $"{{ Id={x.NodeId}, State={x.NodeState}, Succ={x.Successor}, Pred={x.Predecessor}}}");
            _logger.WriteLine($"ring {++i}: nodes {string.Join(", ", nodeDescriptions)}");
        }
    }

    private IEnumerable<IEnumerable<ChordNode>> chordNetworkStructure(
        IEnumerable<ChordNode> nodes)
    {
        var node = nodes.First();
        var nodesById = nodes.ToDictionary(x => x.NodeId);

        var ring = new List<ChordNode>() { node };
        var unvisitedNodeIds = nodes.Select(x => x.NodeId).ToHashSet();
        unvisitedNodeIds.Remove(node.NodeId);

        while (unvisitedNodeIds.Any())
        {
            var succId = node.Successor.NodeId;
            if (unvisitedNodeIds.Contains(succId))
            {
                node = nodesById[succId];
                ring.Add(node);
                unvisitedNodeIds.Remove(succId);
            }
            else
            {
                yield return ring;
                var nextId = unvisitedNodeIds.FirstOrDefault();
                // TODO: if default value of ChordKey is assigned to a node, this produces an error
                //       -> use Maybe monad instead

                if (nodesById.ContainsKey(nextId))
                {
                    node = nodesById[nextId];
                    ring = new List<ChordNode>() { node };
                    unvisitedNodeIds.Remove(nextId);
                }
            }
        }
    }
}
