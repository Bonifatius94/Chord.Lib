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
        /// Send a UDP message without waiting for a response.
        /// </summary>
        /// <param name="local">The local endpoint sending the request.</param>
        /// <param name="remote">The remote endpoint receiving the request.</param>
        /// <param name="message">The message to be sent.</param>
        public void ExecuteNoResponse(IPEndPoint local, IPEndPoint remote, ChordMessage message)
        {
            // create request message
            var datagram = ChordMessageFactory.GetAsBinary(message);

            // open udp client for sending / receiving messages
            using (var client = new UdpClient(local))
            {
                // send the request
                client.Connect(remote);
                client.Send(datagram, datagram.Length);
            }
        }

        /// <summary>
        /// Send a UDP message, wait for a response asynchronously and return the response.
        /// </summary>
        /// <param name="local">The local endpoint sending the request.</param>
        /// <param name="remote">The remote endpoint receiving the request.</param>
        /// <param name="message">The message to be sent.</param>
        /// <returns>the response message to the given request</returns>
        public async Task<ChordMessage> ExecuteWithResponse(IPEndPoint local, IPEndPoint remote, ChordMessage message)
        {
            ChordMessage response = null;

            // create request message
            var datagram = ChordMessageFactory.GetAsBinary(message);

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
                while (!(response.LookupKey.Equals(message.LookupKey)
                    && response.RequestId.Equals(message.RequestId)));
            }

            return response;
        }

        #endregion Methods
    }
}
