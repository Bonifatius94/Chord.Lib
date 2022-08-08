using System;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Lib;

public class ChordBootstrapper
{
    public ChordBootstrapper(IIpSettings ipConfig)
       => this.ipConfig = ipConfig;

    private readonly IIpSettings ipConfig;

    // TODO: think of making this async and cancellable
    public IChordEndpoint FindBootstrapNode(
        Func<IChordRequestMessage, IChordEndpoint, Task<IChordResponseMessage>> sendRequest)
    {
        const int timeout = 10000;

        // expect all chord nodes to use the same port
        // expect something like an exclusive LAN for chord nodes

        // get network id and broadcast address
        var networkId = ipConfig.GetIpv4NetworkId();
        var broadcast = ipConfig.GetIpv4Broadcast();
        var chordPort = ipConfig.GetChordPort();
        // TODO: think of using ASP.NET helper functions instead

        // determine the first and last address in the address space
        var firstIp = new BigInteger(networkId.GetAddressBytes()) + 1;
        var lastIp = new BigInteger(broadcast.GetAddressBytes()) - 1;

        // loop through all possible node addresses in the network
        for (BigInteger addr = firstIp; addr <= lastIp; addr++)
        {
            // define the chord endpoint to be tried out
            var targetEndpoint = new ChordEndpoint() {
                NodeId = -1,
                IpAddress = new IPAddress(addr.ToByteArray()).ToString(),
                Port = chordPort.ToString()
            };

            // send chord health check requests
            var cancelCallback = new CancellationTokenSource();
            var requestTask = Task.Run(() => sendRequest(
                    new ChordRequestMessage() {
                        Type = ChordRequestType.HealthCheck,
                        RequesterId = -1
                    },
                    targetEndpoint
                ), cancelCallback.Token
            );

            // time out the request after several seconds
            var timeoutTask = Task.Delay(timeout);
            var first = Task.WhenAny(requestTask, timeoutTask);
            bool bootstrapFound = first != timeoutTask;

            // cancel the request after a timeout and try another IP
            if (!bootstrapFound) { cancelCallback.Cancel(); continue; }

            // a bootstrapper was found
            return requestTask.Result.Responder;
        }

        return null;
    }
}
