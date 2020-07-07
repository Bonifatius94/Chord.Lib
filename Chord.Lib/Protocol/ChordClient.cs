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
        /// Perform a UDP request that finds the node managing the given chord hash, starting at the given bootstrap chord peer.
        /// </summary>
        /// <param name="local">The local IP endpoint settings.</param>
        /// <param name="bootstrap">The IP endpoint settings of the remote peer where the initial request is sent to.</param>
        /// <param name="key">The key to look up.</param>
        /// <returns>an awaitable chord response message.</returns>
        public async Task<ChordMessage> FindSuccessor(ChordEndpoint local, ChordEndpoint bootstrap, BigInteger key)
        {
            var request = new ChordMessage(local, key);
            return await executeRequest(local.Endpoint, bootstrap.Endpoint, request);
        }

        /// <summary>
        /// Perform a UDP request for joining the chord network.
        /// </summary>
        /// <param name="local">The local IP endpoint settings.</param>
        /// <param name="successorOrPredecessor">The IP endpoint settings of the successor or predecessor to be joined.</param>
        /// <returns>an awaitable chord response message.</returns>
        public async Task<ChordMessage> JoinNetwork(ChordEndpoint local, ChordEndpoint successorOrPredecessor)
        {
            var request = new ChordMessage(local);
            return await executeRequest(local.Endpoint, successorOrPredecessor.Endpoint, request);
        }

        private async Task<ChordMessage> executeRequest(IPEndPoint local, IPEndPoint remote, ChordMessage request)
        {
            ChordMessage response = null;

            // create request message
            var datagram = ChordMessageFactory.GetAsBinary(request);

            // open udp client for sending / receiving messages
            using (var client = new UdpClient(local))
            {
                // send the request
                client.Connect(remote);
                await client.SendAsync(datagram, datagram.Length);

                // TODO: check if using one udp client works for different request / response remotes 
                // (should work because client.Bind() was not executed after connecting)

                UdpReceiveResult result;

                // wait for the response
                do
                {
                    // wait for a response
                    result = await client.ReceiveAsync();

                    // parse the response content
                    response = ChordMessageFactory.FromBinary(result.Buffer);
                }
                // make sure that the response matches the request id
                while (!(response.LookupKey.Equals(request.LookupKey) && response.RequestId.Equals(request.RequestId)));
            }

            return response;
        }

        #endregion Methods
    }
}
