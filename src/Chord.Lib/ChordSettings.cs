using System.Net;

namespace Chord.Lib;

public interface IIpSettings
{
    /// <summary>
    /// Retrieve the chord node's IP address associated with the chord network's CIDR.
    /// (default: IP address from the first non-localhost notwork interface detected)
    /// </summary>
    /// <returns>the IP address specified in settings</returns>
    IPAddress GetChordIpv4Address();

    /// <summary>
    /// Retrieve the chord port from environment variable CHORD_PORT. (default: 9876)
    /// </summary>
    /// <returns>the network port specified in node settings as string</returns>
    int GetChordPort();

    IPAddress GetIpv4NetworkId();

    IPAddress GetIpv4Broadcast();
}