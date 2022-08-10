using System;
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
    class RequestSenderMock : IChordClient
    {
        public RequestSenderMock(string successIp)
            => this.successIp = successIp;

        private string successIp;

        public async Task<IChordResponseMessage> SendRequest(
            IChordRequestMessage request, IChordEndpoint receiver, CancellationToken? token = null)
        {
            if (receiver.IpAddress.Equals(successIp))
                return new ChordResponseMessage() { Responder = receiver };

            await Task.Delay(2000);
            throw new TimeoutException($"request for { receiver } timed out!");
        }
    }

    public BootstrapperTest()
    {
        ipConfigMock = Substitute.For<IIpSettings>();
    }

    private IIpSettings ipConfigMock;

    [Fact]
    public async Task Test_ShouldFindBootstrapNode_WhenHealthCheckSuccessful()
    {
        ipConfigMock.ChordPort.Returns(9876);
        ipConfigMock.IPv4NetworkId.Returns(IPAddress.Parse("192.168.178.0"));
        ipConfigMock.IPv4Broadcast.Returns(IPAddress.Parse("192.168.178.255"));

        string expBootstrapIp = "192.168.178.243";
        var senderMock = new RequestSenderMock(expBootstrapIp);

        var endpointGen = new IPv4EndpointGenerator(
            ipConfigMock, (k) => new ChordKey(k, 254));
        var sut = new ChordBootstrapper(senderMock, endpointGen);
        var bootstrapNode = await sut.FindBootstrapNode();

        bootstrapNode.IpAddress.Should().Be(expBootstrapIp);
    }
}
