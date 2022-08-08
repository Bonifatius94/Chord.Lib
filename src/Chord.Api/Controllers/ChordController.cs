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
            var requestSender = new HttpChordRequestSender();
            Func<IChordRequestMessage, IChordEndpoint, Task<IChordResponseMessage>> sendRequest =
                (request, endpoint) => requestSender.SendRequest(request, endpoint);

            // define a function callback for finding bootstrap nodes
            var bootstrapper = new ChordBootstrapper(ipConfig);
            Func<Task<IChordEndpoint>> bootstrapFunc =
                () => Task.Run(() => bootstrapper.FindBootstrapNode(sendRequest));

            // join the chord network using a bootstrap node
            string localIp = ipConfig.GetChordIpv4Address().ToString();
            string localPort = ipConfig.GetChordPort().ToString();
            node = new ChordNode(sendRequest, localIp, localPort);
            node.JoinNetwork(bootstrapFunc).Wait();
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
