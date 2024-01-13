using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib.Impl;
using FluentAssertions;
using Xunit;

namespace Chord.Lib.Test;

public class ChordProtocolTests
{
    [Fact]
    public void OnNetworkJoin_WhenNodeIsOnlyNode_ThenInitFingerTableWithSelfRefs()
    {
        var virtNetwork = new VirtualIPv4Network();
        virtNetwork.PopulateNetwork(1);
        var node = virtNetwork.Nodes[0];
        var bootstrapper = new ChordBootstrapper(
            virtNetwork.Nodes.Select(n => n.Local));

        var token = new CancellationTokenSource();
        node.JoinNetwork(bootstrapper, token.Token).Wait();

        node.FingerTable.AllFingers.Should().Match(
            t => t.All(f => f.NodeId == node.NodeId));
    }

    [Fact(Skip="figure out why it's not working")]
    public void OnNetworkJoin_WhenNetworkContainsOneNode_ThenInitFingerTableWithOtherNode()
    {
        var virtNetwork = new VirtualIPv4Network();
        virtNetwork.PopulateNetwork(2);
        var node1 = virtNetwork.Nodes[0];
        var node2 = virtNetwork.Nodes[1];
        var bootstrapper1 = new ChordBootstrapper(
            new IChordEndpoint[] { node1.Local });
        var bootstrapper2 = new ChordBootstrapper(
            virtNetwork.Nodes.Select(n => n.Local));

        var token = new CancellationTokenSource();
        node1.JoinNetwork(bootstrapper1, token.Token).Wait();
        node2.JoinNetwork(bootstrapper2, token.Token).Wait();
        Task.Delay(2000).Wait();

        node2.FingerTable.AllFingers.Should().Match(
            t => t.All(f => f.NodeId == node1.NodeId));
        node1.FingerTable.AllFingers.Should().Match(
            t => t.All(f => f.NodeId == node2.NodeId));
    }
}
