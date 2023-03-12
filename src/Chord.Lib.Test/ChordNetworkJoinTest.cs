using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib.Impl;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chord.Lib.Test;

public class SingleNodeJoinTest
{
    const long KEY_SPACE = long.MaxValue;

    private IEnumerable<IChordEndpoint> GenerateEndpoints(int count)
        => Enumerable.Range(1, count)
            .Select(i => $"10.0.0.{i}")
            .Select(ip => new IPv4Endpoint(ip, "9000", KEY_SPACE));

    [Fact]
    public async Task JoinInitialTwoNodesSync()
    {
        int numNodes = 2;
        var httpClient = new IPv4NetworkMock();
        var endpoints = GenerateEndpoints(numNodes).ToList();
        var bootstrapper = Substitute.For<IChordBootstrapper>();
        bootstrapper.FindBootstrapNode(default, default)
            .ReturnsForAnyArgs(args => endpoints[0]);

        // TODO: replace with real worker
        var payloadWorker = new ZeroProtocolPayloadWorker();
        var nodeConfig = new ChordNodeConfiguration();
        var node1 = new ChordNode(endpoints[0], httpClient, payloadWorker, nodeConfig);
        var node2 = new ChordNode(endpoints[1], httpClient, payloadWorker, nodeConfig);
        httpClient.Nodes = new List<ChordNode>{ node1, node2 };

        var cancelCallback = new CancellationTokenSource();
        await node1.JoinNetwork(bootstrapper, cancelCallback.Token);
        await node2.JoinNetwork(bootstrapper, cancelCallback.Token);

        node1.Successor.NodeId.Should().BeEquivalentTo(node2.Local.NodeId);
        node1.Predecessor.NodeId.Should().BeEquivalentTo(node2.Local.NodeId);
        node2.Successor.NodeId.Should().BeEquivalentTo(node1.Local.NodeId);
        node2.Predecessor.NodeId.Should().BeEquivalentTo(node1.Local.NodeId);
    }

    [Fact]
    public async Task JoinInitialTwoNodesParallel()
    {
        int numNodes = 2;
        var httpClient = new IPv4NetworkMock();
        var endpoints = GenerateEndpoints(numNodes).ToList();
        var bootstrapper = Substitute.For<IChordBootstrapper>();
        bootstrapper.FindBootstrapNode(default, default)
            .ReturnsForAnyArgs(args => endpoints[0]);

        // TODO: replace with real worker
        var payloadWorker = new ZeroProtocolPayloadWorker();
        var nodeConfig = new ChordNodeConfiguration();
        var node1 = new ChordNode(endpoints[0], httpClient, payloadWorker, nodeConfig);
        var node2 = new ChordNode(endpoints[1], httpClient, payloadWorker, nodeConfig);
        httpClient.Nodes = new List<ChordNode>{ node1, node2 };

        var cancelCallback = new CancellationTokenSource();
        var join1 = node1.JoinNetwork(bootstrapper, cancelCallback.Token);
        var join2 = node2.JoinNetwork(bootstrapper, cancelCallback.Token);
        await Task.WhenAll(join1, join2);

        node1.Successor.NodeId.Should().BeEquivalentTo(node2.Local.NodeId);
        node1.Predecessor.NodeId.Should().BeEquivalentTo(node2.Local.NodeId);
        node2.Successor.NodeId.Should().BeEquivalentTo(node1.Local.NodeId);
        node2.Predecessor.NodeId.Should().BeEquivalentTo(node1.Local.NodeId);
    }
}
