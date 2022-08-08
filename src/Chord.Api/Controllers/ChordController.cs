using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Chord.Lib;

namespace Chord.Api.Controllers
{
    // TODO: use minimal APIs to get rid of the bloat

    [ApiController]
    [Route("[controller]")]
    public class ChordController : ControllerBase
    {
        public ChordController(IIpSettings ipConfig)
        {
            const long MAX_ID = long.MaxValue;

            var nodeConfig = new ChordNodeConfiguration() {
                IpAddress = ipConfig.GetChordIpv4Address().ToString(),
                ChordPort = ipConfig.GetChordPort().ToString()
            };

            var requestSender = new HttpChordRequestSender();
            var bootstrapper = new IPv4VlanBootstrapper(
                ipConfig, (key) => new ChordKey(key, MAX_ID));

            node = new ChordNode(requestSender, nodeConfig);
            node.JoinNetwork(bootstrapper).Wait();
        }

        private static ChordNode node;

        [HttpPost]
        public async Task<ChordResponseMessage> PostMessage([FromBody] ChordRequestMessage request)
        {
            // let the chord node process the incoming request and return the response
            return (ChordResponseMessage) await node.ProcessRequest(request);

            // TODO: add error handling for timeouts, etc.
        }
    }
}
