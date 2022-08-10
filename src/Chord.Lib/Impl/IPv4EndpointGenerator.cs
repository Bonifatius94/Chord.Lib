namespace Chord.Lib.Impl;

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
            .Select(addr => new ChordEndpoint(
                newKey(-1),
                ChordHealthStatus.Questionable,
                new IPAddress(addr.ToByteArray()).ToString(),
                chordPort.ToString()
            ));
        return allEndpoints;
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
