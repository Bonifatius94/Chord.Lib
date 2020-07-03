using Chord.Lib.Message;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Chord.Lib.Protocol
{
    /// <summary>
    /// An implementation of the chord client.
    /// </summary>
    public class ChordClient
    {
        #region Methods

        /// <summary>
        /// Perform a UDP request that looks up the given chord hash, starting at the given bootstrap chord peer.
        /// </summary>
        /// <param name="local">The local IP endpoint settings.</param>
        /// <param name="bootstrap">The IP endpoint settings of the remote peer where the initial request is sent to.</param>
        /// <param name="key">The key to look up.</param>
        /// <returns>an awaitable result of the key's managing node identifier.</returns>
        public async Task<BigInteger> LookupKey(IPEndPoint local, IPEndPoint bootstrap, BigInteger key)
        {
            BigInteger nodeId;

            // create request message
            var request = new ChordKeyLookupJsonRequestMessage(local, key);
            var datagram = request.GetAsBinary();

            // open udp client for sending / receiving messages
            using (var client = new UdpClient(local))
            {
                // send the request
                client.Connect(bootstrap);
                await client.SendAsync(datagram, datagram.Length);

                // TODO: check if using one udp client works for different request / response remotes (should work because client.Bind() was not executed after connecting)

                UdpReceiveResult result;
                ChordKeyLookupJsonResponseMessage response;

                // wait for the response
                do
                {
                    // wait for a response
                    result = await client.ReceiveAsync();

                    // parse the response content
                    string json = Encoding.UTF8.GetString(result.Buffer);
                    response = JsonConvert.DeserializeObject<ChordKeyLookupJsonResponseMessage>(json);
                }
                while (!(response.LookupKey.Equals(request.LookupKey) && response.RequestId.Equals(request.RequestId)));

                // calculate managing node from response endpoint
                var hash = HashingHelper.GetSha1Hash(result.RemoteEndPoint);
                nodeId = new BigInteger(hash);
            }

            return nodeId;
        }

        #endregion Methods
    }
}
