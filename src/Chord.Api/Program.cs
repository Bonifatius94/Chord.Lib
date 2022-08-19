using System.Threading;
using System.Threading.Tasks;
using Chord.Api;
using Chord.Config;
using Chord.Lib;
using Chord.Lib.Impl;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

var app = WebApplication.Create();
var node = await ConfigureChord();
app.MapPost("/", async ([FromBody] ChordRequestMessage request, CancellationToken token)
    => new OkObjectResult((ChordResponseMessage)await node.ProcessRequest(request, token)));
app.Run();

async Task<ChordNode> ConfigureChord()
{
    const long keySpace = long.MaxValue;

    var ipConfig = new IpSettings();
    var localEndpoint = new IPv4Endpoint(
        ChordKey.PickRandom(keySpace),
        ChordHealthStatus.Starting,
        ipConfig.ChordIpv4Address.ToString(),
        ipConfig.ChordPort.ToString());

    var httpClient = new HttpChordClient();
    var endpointGenerator = new IPv4EndpointGenerator(
        ipConfig, (key) => new ChordKey(key, keySpace));
    var bootstrapper = new ChordBootstrapper(endpointGenerator);

    // TODO: replace with real worker
    var payloadWorker = new ZeroProtocolPayloadWorker();
    var nodeConfig = new ChordNodeConfiguration();
    var node = new ChordNode(localEndpoint, httpClient, payloadWorker, nodeConfig);

    var cancelCallback = new CancellationTokenSource();
    int timeoutMillis = 100;
    while (node.NodeState != ChordHealthStatus.Idle)
    {
        var joinTask = node.JoinNetwork(bootstrapper, cancelCallback.Token);
        await joinTask.Timeout(timeoutMillis, cancelCallback.Token);
        await Task.Delay(timeoutMillis);
        timeoutMillis *= 2;
    }

    return node;
}
