using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Chord.Config
{
    /// <summary>
    /// Helper for retrieving network settings (IP address and port) of the node.
    /// </summary>
    public static class IpSettings
    {
        #region Constants

        /// <summary>
        /// The name of the environment variable specifying the chord network CIDR.
        /// </summary>
        public const string ENV_SETTING_CHORD_NETWORK_CIDR = "CHORD_NETWORK_CIDR";

        /// <summary>
        /// The name of the environment variable specifying the chord port.
        /// </summary>
        public const string ENV_SETTING_CHORD_PORT = "CHORD_PORT";

        /// <summary>
        /// A regular expression for validating network CIDR format.
        /// </summary>
        private const string REGEX_NETWORK_CIDR =
            "^([0-9]{1,3}\\.){3}[0-9]{1,3}(\\/([0-9]|[1-2][0-9]|3[0-2]))?$";

        #endregion Constants

        #region Methods

        /// <summary>
        /// Retrieve the chord node's IP address using the given chord network CIDR from CHORD_NETWORK_CIDR environment variable.
        /// (default: IP address from the first non-localhost notwork interface detected)
        /// </summary>
        /// <returns>the IP address specified in settings</returns>
        public static IPAddress GetChordIpv4Address()
        {
            // TODO: add feature to find IP address alternatively by the docker network name

            // snippet source: https://stackoverflow.com/questions/6803073/get-local-ip-address

            // load chord network CIDR setting from environment variable (defaults to null if variable is not found)
            string networkCidr = Environment.GetEnvironmentVariable(ENV_SETTING_CHORD_NETWORK_CIDR);

            // get all IP addresses of ethernet controllers plugged to the node
            var ipAddresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToList();

            // determine the first IP address that is part of the given network mask (default: use first IP address available)
            var chordIpv4Address = networkCidr != null
                ? ipAddresses.FirstOrDefault(address => isPartOfNetwork(address.ToString(), networkCidr)) 
                : ipAddresses.FirstOrDefault();

            // make sure that an IP address was found
            if (string.IsNullOrEmpty(chordIpv4Address?.ToString())) { throw new IOException(
                "No suitable ethernet interface detected! Cannot connect to other peers!"); }

            return chordIpv4Address;
        }

        /// <summary>
        /// Retrieve the chord port from environment variable CHORD_PORT. (default: 9876)
        /// </summary>
        /// <returns>the network port specified in node settings as string</returns>
        public static int GetChordPort()
        {
            // load chord port setting from environment variable (defaults to '9876' if variable is not found)
            const string CHORD_DEFAULT_PORT = "9876";
            return int.Parse(Environment.GetEnvironmentVariable(ENV_SETTING_CHORD_PORT) ?? CHORD_DEFAULT_PORT);
        }

        public static IPAddress GetIpv4NetworkId()
        {
            // load chord network CIDR setting from environment variable
            string networkCidr = Environment.GetEnvironmentVariable(ENV_SETTING_CHORD_NETWORK_CIDR);

            // make sure that the environment variable is specified, otherwise it won't work
            if (string.IsNullOrEmpty(networkCidr)) { throw new IOException(
                "Network CIDR environment variable is not specified! Cannot continue without it!"); }

            // make sure that the network CIDR mask is valid
            if (!Regex.IsMatch(networkCidr, REGEX_NETWORK_CIDR)) { throw new ArgumentException(
                "Invalid network cidr argument! Please only put IPv4 compatibe network CIDR masks."); }

            // split CIDR network mask at '/' separator
            string[] parts = networkCidr.Split('/');
            string networkId = parts[0];
            int networkBitsCount = int.Parse(parts[1]);

            // get numeric representation of network id, ip address and subnet mask
            int networkIdBytes = BitConverter.ToInt32(IPAddress.Parse(networkId).GetAddressBytes(), 0);
            int subnetMask = IPAddress.HostToNetworkOrder(-1 << (32 - networkBitsCount));

            // compute the network address bitwise and return it as IP address object
            return new IPAddress(networkIdBytes & subnetMask);
        }

        public static IPAddress GetIpv4Broadcast()
        {
            // load chord network CIDR setting from environment variable
            string networkCidr = Environment.GetEnvironmentVariable(ENV_SETTING_CHORD_NETWORK_CIDR);

            // make sure that the environment variable is specified, otherwise it won't work
            if (string.IsNullOrEmpty(networkCidr)) { throw new IOException(
                "Network CIDR environment variable is not specified! Cannot continue without it!"); }

            // make sure that the network CIDR mask is valid
            if (!Regex.IsMatch(networkCidr, REGEX_NETWORK_CIDR)) { throw new ArgumentException(
                "Invalid network cidr argument! Please only put IPv4 compatibe network CIDR masks."); }

            // split CIDR network mask at '/' separator
            string[] parts = networkCidr.Split('/');
            string networkId = parts[0];
            int networkBitsCount = int.Parse(parts[1]);

            // get numeric representation of network id and subnet mask
            int networkIdBytes = BitConverter.ToInt32(IPAddress.Parse(networkId).GetAddressBytes(), 0);
            int subnetMask = IPAddress.HostToNetworkOrder(-1 << (32 - networkBitsCount));

            // compute the broadcast using bitwise operations and return it as IP address object
            return new IPAddress((networkIdBytes & subnetMask) | ~subnetMask);
        }

        #region Helpers

        /// <summary>
        /// Determine whether given the IP address is part of the given network.
        /// </summary>
        /// <param name="ipAddress">The IP address to evaluate.</param>
        /// <param name="networkCidr">The network (in CIDR notation) to evaluate.</param>
        /// <returns>a boolean whether given the IP address is part of given the network</returns>
        private static bool isPartOfNetwork(string ipAddress, string networkCidr)
        {
            // snippet source: https://stackoverflow.com/questions/9622967/how-to-see-if-an-ip-address-belongs-inside-of-a-range-of-ips-using-cidr-notation
            // regex source: https://www.regextester.com/93987

            // TODO: check if the regex works

            // make sure that the mask is valid
            if (!Regex.IsMatch(networkCidr, REGEX_NETWORK_CIDR)) { throw new ArgumentException(
                "Invalid network cidr argument! Please only put IPv4 compatibe network CIDR masks."); }

            // split CIDR network mask at '/' separator
            string[] parts = networkCidr.Split('/');
            string networkId = parts[0];
            int networkBitsCount = int.Parse(parts[1]);

            // get numeric representation of network id, ip address and subnet mask
            int networkIdBytes = BitConverter.ToInt32(IPAddress.Parse(networkId).GetAddressBytes(), 0);
            int ipAddressBytes = BitConverter.ToInt32(IPAddress.Parse(ipAddress).GetAddressBytes(), 0);
            int subnetMask = IPAddress.HostToNetworkOrder(-1 << (32 - networkBitsCount));

            // determine whether the given IP address is part of the given network CIDR
            return (networkIdBytes & subnetMask) == (ipAddressBytes & subnetMask);
        }

        #endregion Helpers

        #endregion Methods
    }
}
