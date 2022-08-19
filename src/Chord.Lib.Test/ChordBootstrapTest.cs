using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib.Impl;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chord.Lib.Test.BootstrapperTest;

public class BootstrapperTest
{
    class SpecificSuccessfulPingRequestSenderMock : IChordClient
    {
        public SpecificSuccessfulPingRequestSenderMock(string successIp)
            => this.successIp = successIp;

        private string successIp;

        public async Task<IChordResponseMessage> SendRequest(
            IChordRequestMessage request, IChordEndpoint receiver, CancellationToken token)
        {
            if (receiver.IpAddress.Equals(successIp))
            {
                return new ChordResponseMessage() {
                    Responder = new IPv4Endpoint(
                        receiver.NodeId,
                        ChordHealthStatus.Idle,
                        receiver.IpAddress,
                        receiver.Port)
                };
            }

            await Task.Delay(2000);
            throw new TimeoutException($"request for { receiver } timed out!");
        }
    }

    class AllPingsTimeoutRequestSenderMock : IChordClient
    {
        public async Task<IChordResponseMessage> SendRequest(
            IChordRequestMessage request, IChordEndpoint receiver, CancellationToken token)
        {
            await Task.Delay(2000);
            throw new TimeoutException($"request for { receiver } timed out!");
        }
    }

    class AllPingsThrowRequestSenderMock : IChordClient
    {
        public async Task<IChordResponseMessage> SendRequest(
            IChordRequestMessage request, IChordEndpoint receiver, CancellationToken token)
        {
            await Task.Delay(20);
            throw new TimeoutException($"DNS error for { receiver }!");
        }
    }

    public BootstrapperTest()
    {
        ipConfigMock = Substitute.For<IIpSettings>();
        ipConfigMock.ChordPort.Returns(9876);
        ipConfigMock.IPv4NetworkId.Returns(IPAddress.Parse("192.168.178.0"));
        ipConfigMock.IPv4Broadcast.Returns(IPAddress.Parse("192.168.178.255"));
    }

    private IIpSettings ipConfigMock;
    private const int keySpace = 100000;

    [Fact]
    public async Task Test_ShouldFindBootstrapNode_WhenHealthCheckSuccessful()
    {
        string expBootstrapIp = "192.168.178.243";
        var clientMock = new SpecificSuccessfulPingRequestSenderMock(expBootstrapIp);
        var endpointGen = new IPv4EndpointGenerator(
            ipConfigMock, (k) => new ChordKey(k, keySpace));
        var local = endpointGen.GenerateEndpoints().First();
        var sender = new ChordRequestSender(clientMock, null);

        var sut = new ChordBootstrapper(endpointGen);
        var bootstrapNode = await sut.FindBootstrapNode(sender, local);

        bootstrapNode.Should().NotBeNull();
        bootstrapNode.IpAddress.Should().Be(expBootstrapIp);
    }

    [Fact]
    public async Task Test_ShouldFindNoBootstrapNode_WhenAllPingsTimeOut()
    {
        var clientMock = new AllPingsTimeoutRequestSenderMock();
        var endpointGen = new IPv4EndpointGenerator(
            ipConfigMock, (k) => new ChordKey(k, keySpace));
        var local = endpointGen.GenerateEndpoints().First();
        var sender = new ChordRequestSender(clientMock, null);

        var sut = new ChordBootstrapper(endpointGen);
        var bootstrapNode = await sut.FindBootstrapNode(sender, local);

        bootstrapNode.Should().BeNull();
    }

    [Fact]
    public async Task Test_ShouldFindNoBootstrapNode_WhenAllPingsThrowAnException()
    {
        var clientMock = new AllPingsThrowRequestSenderMock();
        var endpointGen = new IPv4EndpointGenerator(
            ipConfigMock, (k) => new ChordKey(k, keySpace));
        var local = endpointGen.GenerateEndpoints().First();
        var sender = new ChordRequestSender(clientMock, null);

        var sut = new ChordBootstrapper(endpointGen);
        var bootstrapNode = await sut.FindBootstrapNode(sender, local);

        bootstrapNode.Should().BeNull();
    }
}
