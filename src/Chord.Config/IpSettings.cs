using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Chord.Lib;

namespace Chord.Config
{
    public class IpSettings : IIpSettings
    {
        #region Defaults

        private const string CHORD_DEFAULT_PORT = "9876";

        #endregion Defaults

        #region Environment

        private const string ENV_SETTING_CHORD_NETWORK_CIDR = "CHORD_NETWORK_CIDR";
        private const string ENV_SETTING_CHORD_PORT = "CHORD_PORT";

        private string networkCidr
            => Environment.GetEnvironmentVariable(ENV_SETTING_CHORD_NETWORK_CIDR);

        private string chordPort
            => Environment.GetEnvironmentVariable(ENV_SETTING_CHORD_PORT);

        private bool isCidrConfigured(string cidr, Action<string> onError = null)
        {
            // make sure that the environment variable is specified, otherwise it won't work
            if (string.IsNullOrEmpty(networkCidr))
            {
                const string message = "Network CIDR environment "
                    + "variable is not specified! Cannot continue without it!";
                onError?.Invoke(message);
                return false;
            }

            // make sure that the network CIDR mask is valid
            if (!networkCidr.IsValidIPv4CIDR())
            {
                const string message = "Invalid network cidr argument! "
                    + "Please only put IPv4 compatibe network CIDR masks.";
                onError?.Invoke(message);
                return false;
            }

            return true;
        }

        private (string, int) splitCidr(string cidr)
            => (cidr.Split('/')[0], int.Parse(cidr.Split('/')[1]));

        #endregion Environment

        #region ChordNode

        public IPAddress GetChordIpv4Address()
        {
            // TODO: add feature to find IP address alternatively by the docker network name

            var ipAddresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToList();

            var chordIpv4Address = !isCidrConfigured(networkCidr) ? ipAddresses.FirstOrDefault()
                : ipAddresses.FirstOrDefault(address => address.IsPartOfNetwork(networkCidr));

            if (string.IsNullOrEmpty(chordIpv4Address?.ToString())) { throw new IOException(
                "No suitable ethernet interface detected! Cannot connect to other peers!"); }

            return chordIpv4Address;
        }

        public int GetChordPort()
            => int.Parse(chordPort ?? CHORD_DEFAULT_PORT);

        #endregion ChordNode

        #region ChordNetwork

        public IPAddress GetIpv4NetworkId()
        {
            // parse network CIDR
            isCidrConfigured(networkCidr, (msg) => throw new ArgumentException(msg));
            var (networkId, networkBitsCount) = splitCidr(networkCidr);

            // get numeric representation of network id, ip address and subnet mask
            int networkIdBytes = BitConverter.ToInt32(IPAddress.Parse(networkId).GetAddressBytes(), 0);
            int subnetMask = IPAddress.HostToNetworkOrder(-1 << (32 - networkBitsCount));

            // compute the network address bitwise and return it as IP address object
            return new IPAddress(networkIdBytes & subnetMask);
        }

        public IPAddress GetIpv4Broadcast()
        {
            // parse network CIDR
            isCidrConfigured(networkCidr, (msg) => throw new ArgumentException(msg));
            var (networkId, networkBitsCount) = splitCidr(networkCidr);

            // get numeric representation of network id and subnet mask
            int networkIdBytes = BitConverter.ToInt32(IPAddress.Parse(networkId).GetAddressBytes(), 0);
            int subnetMask = IPAddress.HostToNetworkOrder(-1 << (32 - networkBitsCount));

            // compute the broadcast using bitwise operations and return it as IP address object
            return new IPAddress((networkIdBytes & subnetMask) | ~subnetMask);
        }

        #endregion ChordNetwork
    }

    public static class IPAddressEx
    {
        private const string REGEX_NETWORK_CIDR =
            "^([0-9]{1,3}\\.){3}[0-9]{1,3}(\\/([0-9]|[1-2][0-9]|3[0-2]))?$";

        // snippet source: https://stackoverflow.com/questions/9622967/how-to-see-if-an-ip-address-belongs-inside-of-a-range-of-ips-using-cidr-notation
        // regex source: https://www.regextester.com/93987

        // TODO: check if the regex works
        public static bool IsValidIPv4CIDR(this string cidrAddress)
            => Regex.IsMatch(cidrAddress, REGEX_NETWORK_CIDR);

        public static bool IsPartOfNetwork(this IPAddress address, string networkCidr)
        {
            if (!networkCidr.IsValidIPv4CIDR()) { throw new ArgumentException(
                "Invalid network cidr argument! Please only put IPv4 compatibe network CIDR masks."); }

            // split CIDR network mask at '/' separator
            string[] parts = networkCidr.Split('/');
            string networkId = parts[0];
            int networkBitsCount = int.Parse(parts[1]);

            // get numeric representation of network id, ip address and subnet mask
            int networkIdBytes = BitConverter.ToInt32(IPAddress.Parse(networkId).GetAddressBytes(), 0);
            int ipAddressBytes = BitConverter.ToInt32(address.GetAddressBytes(), 0);
            int subnetMask = IPAddress.HostToNetworkOrder(-1 << (32 - networkBitsCount));

            // determine whether the given IP address is part of the given network CIDR
            return (networkIdBytes & subnetMask) == (ipAddressBytes & subnetMask);
        }
    }
}
