using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib;

namespace Chord.Api;

public class HttpChordClient : IChordRequestProcessor
{
    public async Task<IChordResponseMessage> ProcessRequest(
        IChordRequestMessage request, CancellationToken token)
    {
        var receiver = request.Receiver;
        string url = $"http://{ receiver.IpAddress }:{ receiver.Port }/chord";
        string requestJson = JsonSerializer.Serialize(request); // TODO: check if this works

        using (var client = new HttpClient())
        using (var content = new StringContent(requestJson))
        {
            // send the request as JSON, parse the response from JSON
            var httpResponse = await client.PostAsync(url, content, token);
            var responseJson = await httpResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ChordResponseMessage>(responseJson);
        }

        // TODO: keep the TCP connections open as long as possible
    }
}
