using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib;

namespace Chord.Api.Controllers;

public class HttpChordRequestSender : IChordClient
{
    public async Task<IChordResponseMessage> SendRequest(
        IChordRequestMessage request, IChordEndpoint receiver, CancellationToken? token)
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
}
