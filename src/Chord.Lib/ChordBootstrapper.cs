using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Lib;

public interface IExplorableChordEndpointGenerator
{
    IEnumerable<IChordEndpoint> GenerateEndpoints();
}

public class IPv4EndpointGenerator : IExplorableChordEndpointGenerator
{
    public IPv4EndpointGenerator(
        IIpSettings ipConfig,
        Func<BigInteger, ChordKey> newKey)
    {
        this.ipConfig = ipConfig;
        this.newKey = newKey;
    }

    private readonly IIpSettings ipConfig;
    private readonly Func<BigInteger, ChordKey> newKey;

    private (BigInteger, BigInteger) getFirstAndLastAddress()
    {
        // expect all chord nodes to use the same port
        // expect something like an exclusive LAN for chord nodes

        // get network id and broadcast address
        var networkId = ipConfig.Ipv4NetworkId;
        var broadcast = ipConfig.Ipv4Broadcast;

        // determine the first and last address in the address space
        var firstIp = new BigInteger(networkId.GetAddressBytes(), isUnsigned: true) + 1;
        var lastIp = new BigInteger(broadcast.GetAddressBytes(), isUnsigned: true) - 1;
        return (firstIp, lastIp);
    }

    public IEnumerable<IChordEndpoint> GenerateEndpoints()
    {
        var chordPort = ipConfig.ChordPort;
        var (firstIp, lastIp) = getFirstAndLastAddress();
        var allEndpoints = BigIntEnumerable.Range(firstIp, lastIp)
            .Select(addr => new ChordEndpoint() {
                NodeId = newKey(-1),
                IpAddress = new IPAddress(addr.ToByteArray()).ToString(),
                Port = chordPort.ToString()
            });
        return allEndpoints;
    }
}

public class ChordBootstrapper : IChordBootstrapper
{
    public ChordBootstrapper(
        IExplorableChordEndpointGenerator endpointGenerator)
    {
        this.endpointGenerator = endpointGenerator;
    }

    private IExplorableChordEndpointGenerator endpointGenerator;

    public async Task<IChordEndpoint> FindBootstrapNode(IChordRequestSender sender)
    {
        const int PING_TIMEOUT_MS = 1000;
        const int NUM_PARALLEL_PINGS = 128;

        Func<IChordEndpoint, bool> ping = (e) => pingEndpoint(sender, e, PING_TIMEOUT_MS);
        var allEndpoints = endpointGenerator.GenerateEndpoints();

        foreach (var endpointsToPing in allEndpoints.Chunk(NUM_PARALLEL_PINGS))
        {
            var pingTasksByEndpoint = endpointsToPing
                .ToDictionary(x => x, x => Task.Run(() => ping(x)));

            while (pingTasksByEndpoint.Values.Any(x => x.Status == TaskStatus.Running))
            {
                await Task.WhenAny(pingTasksByEndpoint.Values);
                var bootstrapNode = pingTasksByEndpoint
                    .Where(x => x.Value.Status == TaskStatus.RanToCompletion)
                    .FirstOrDefault(x => x.Value.Result).Key;

                if (bootstrapNode != null)
                   return bootstrapNode;
            }
        }

        return null;
    }

    private bool pingEndpoint(
        IChordRequestSender sender,
        IChordEndpoint endpoint,
        int timeoutMillis)
    {
        Action ping = () => sender.SendRequest(
                new ChordRequestMessage() { Type = ChordRequestType.HealthCheck },
                endpoint
            );

        var tokenSource = new CancellationTokenSource();
        var requestTask = Task.Run(ping, tokenSource.Token);

        var timeoutTask = Task.Delay(timeoutMillis);
        bool ranIntoTimeout = Task.WaitAny(timeoutTask, requestTask) == 0;

        if (ranIntoTimeout)
            tokenSource.Cancel();

        return !ranIntoTimeout;
    }
}

public static class BigIntEnumerable
{
    public static IEnumerable<BigInteger> Range(
        BigInteger first, BigInteger last)
    {
        for (BigInteger value = first; value <= last; value++)
            yield return value;
    }
}
