using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Chord.Lib;
using Chord.Lib.Impl;

namespace Chord.Api.Controllers
{
    // TODO: use minimal APIs to get rid of the bloat

    [ApiController]
    [Route("[controller]")]
    public class ChordController : ControllerBase
    {
        public ChordController(IIpSettings ipConfig)
        {
            const long keySpace = long.MaxValue;

            var localEndpoint = new ChordEndpoint(
                ChordKey.PickRandom(keySpace),
                ChordHealthStatus.Starting,
                ipConfig.ChordIpv4Address.ToString(),
                ipConfig.ChordPort.ToString());

            var requestSender = new HttpChordRequestSender();
            var endpointGenerator = new IPv4EndpointGenerator(
                ipConfig, (key) => new ChordKey(key, keySpace));
            var bootstrapper = new ChordBootstrapper(requestSender, endpointGenerator);

            // TODO: replace with real worker
            var payloadWorker = new ZeroProtocolPayloadWorker();
            var nodeConfig = new ChordNodeConfiguration();
            node = new ChordNode(requestSender, payloadWorker, nodeConfig);
            node.JoinNetwork(localEndpoint, bootstrapper).Wait();
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
