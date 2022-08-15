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
        var networkId = ipConfig.IPv4NetworkId;
        var broadcast = ipConfig.IPv4Broadcast;

        // determine the first and last address in the address space
        var firstIp = networkId.ToBigInt() + 1;
        var lastIp = broadcast.ToBigInt() - 1;
        return (firstIp, lastIp);
    }

    public IEnumerable<IChordEndpoint> GenerateEndpoints()
    {
        var chordPort = ipConfig.ChordPort;
        var (firstIp, lastIp) = getFirstAndLastAddress();
        var allEndpoints = BigIntEnumerable.Range(firstIp, lastIp)
            .Select(addr => new IPv4Endpoint(
                newKey(0),
                ChordHealthStatus.Questionable,
                addr.FromBigInt().ToString(),
                chordPort.ToString()
            ));
        return allEndpoints;
    }
}

public static class IPv4ToBigInt
{
    public static BigInteger ToBigInt(this IPAddress address)
        => new BigInteger(
            address.GetAddressBytes().Reverse().ToArray(),
            isUnsigned: true);

    public static IPAddress FromBigInt(this BigInteger addrAsInt)
        => new IPAddress(addrAsInt.ToByteArray().Take(4).Reverse().ToArray());
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
