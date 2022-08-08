using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chord.Lib.Test.BootstrapperTest;

public class BootstrapperTest
{
    class RequestSenderMock : IChordRequestSender
    {
        public RequestSenderMock(IChordResponseMessage successResponse)
            => this.successResponse = successResponse;


        private int i = 0;
        private IChordResponseMessage successResponse;

        public async Task<IChordResponseMessage> SendRequest(
            IChordRequestMessage request, IChordEndpoint receiver)
        {
            if (++i % 244 == 0)
                return successResponse;
            await Task.Delay(2000);
            return null;
        }
    }

    public BootstrapperTest()
    {
        settingsMock = Substitute.For<IIpSettings>();
        responseMock = Substitute.For<IChordResponseMessage>();
        senderMock = new RequestSenderMock(responseMock);
    }

    private IIpSettings settingsMock;
    private IChordRequestSender senderMock;
    private IChordResponseMessage responseMock;

    [Fact]
    public async Task Test_ShouldFindBootstrapNode_WhenHealthCheckSuccessful()
    {
        settingsMock.ChordPort.Returns(9876);
        settingsMock.Ipv4NetworkId.Returns(IPAddress.Parse("192.168.178.0"));
        settingsMock.Ipv4Broadcast.Returns(IPAddress.Parse("192.168.178.255"));

        responseMock.Responder = new ChordEndpoint() {
            IpAddress = "192.168.178.245",
            NodeId = ChordKey.PickRandom(),
            Port = "9876",
            State = ChordHealthStatus.Idle
        };

        var sut = new IPv4VlanBootstrapper(settingsMock, (k) => new ChordKey(k, 254));
        var bootstrapNode = await sut.FindBootstrapNode(senderMock);

        bootstrapNode.IpAddress.Should().Be(responseMock.Responder.IpAddress);
        bootstrapNode.NodeId.Should().Be(responseMock.Responder.NodeId);
        bootstrapNode.Port.Should().Be(responseMock.Responder.Port);
        bootstrapNode.State.Should().Be(responseMock.Responder.State);
    }
}
