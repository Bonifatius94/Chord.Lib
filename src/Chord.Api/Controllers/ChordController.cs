using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Chord.Lib.Core;
using System.Net.Http;
using System.Text.Json;
using Chord.Config;
using System.Numerics;
using System.Net;
using System.Threading;

namespace Chord.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChordController : ControllerBase
    {
        static ChordController()
        {
            // determine the local chord endpoint's IP address and port
            string localIp = IpSettingUtils.GetChordIpv4Address().ToString();
            string localPort = IpSettingUtils.GetChordPort().ToString();

            // join the chord network using a bootstrap node
            var bootstrap = findBootstrapNode();
            node = new ChordNode(sendRequest);
            node.JoinNetwork(bootstrap, localIp, localPort).Wait();
        }

        private static ChordNode node;

        private static IChordEndpoint findBootstrapNode()
        {
            const int timeout = 10000;

            // expect all chord nodes to use the same port
            // expect something like an exclusive LAN for chord nodes

            // get network id and broadcast address
            var networkId = IpSettingUtils.GetIpv4NetworkId();
            var broadcast = IpSettingUtils.GetIpv4Broadcast();
            var chordPort = IpSettingUtils.GetChordPort();
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

        private static async Task<IChordResponseMessage> sendRequest(
            IChordRequestMessage request, IChordEndpoint receiver)
        {
            // create the request URL to the remote chord endpoint
            string url = $"http://{ receiver.IpAddress }:{ receiver.Port }/chord";

            // serialize the request as JSON
            string requestJson = JsonSerializer.Serialize(request); // TODO: check if this works

            // open a HTTP connection to the remote endpoint
            using (var client = new HttpClient())
            using (var content = new StringContent(requestJson))
            {
                // send the request as JSON, parse the response from JSON
                var httpResponse = await client.PostAsync(url, content);
                var responseJson = await httpResponse.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ChordResponseMessage>(responseJson);
            }

            // TODO: keep the TCP connections open as long as possible
            // TODO: add error handling for timeouts, etc.
        }

        [HttpPost]
        public async Task<ChordResponseMessage> PostMessage([FromBody] ChordRequestMessage request)
        {
            // let the chord node process the incoming request and return the response
            return (ChordResponseMessage) await node.ProcessRequest(request);

            // TODO: add error handling for timeouts, etc.
        }
    }
}
