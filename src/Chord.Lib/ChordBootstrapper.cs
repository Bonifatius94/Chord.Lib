namespace Chord.Lib;

public class ChordBootstrapper : IChordBootstrapper
{
    public ChordBootstrapper(
        IChordClient sender,
        IExplorableChordEndpointGenerator endpointGenerator,
        int pingTimeoutMillis = 1000,
        int numParallelPings = 128)
    {
        this.sender = sender;
        this.endpointGenerator = endpointGenerator;
        this.pingTimeoutMillis = pingTimeoutMillis;
        this.numParallelPings = numParallelPings;
    }

    private IChordClient sender;
    private IExplorableChordEndpointGenerator endpointGenerator;
    private int pingTimeoutMillis;
    private int numParallelPings;

    public async Task<IChordEndpoint> FindBootstrapNode()
    {
        var cancelCallback = new CancellationTokenSource();
        Func<IChordEndpoint, Task<IChordEndpoint>> ping =
            async (e) => await pingEndpoint(sender, e, pingTimeoutMillis, cancelCallback.Token);
        var allEndpoints = endpointGenerator.GenerateEndpoints();

        foreach (var endpointsToPing in allEndpoints.Chunk(numParallelPings))
        {
            var pingTasks = endpointsToPing.Select(e => ping(e)).ToArray();

            // first successful task cancels all others
            await Task.WhenAll(pingTasks
                .Select(t =>
                    t.ContinueWith(x => {
                        if (x.Result != null)
                            cancelCallback.Cancel();
                    })));

            var bootstrapNode = pingTasks
                .Select(x => x.Result)
                .FirstOrDefault(x => x != null);

            if (bootstrapNode != null)
                return bootstrapNode;
        }

        return null;
    }

    private async Task<IChordEndpoint> pingEndpoint(
        IChordClient client,
        IChordEndpoint receiver,
        int timeoutMillis,
        CancellationToken token)
    {
        return await client
            // TODO: move healthcheck logic to the ChordRequestSender
            .SendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.HealthCheck
                },
                receiver)
            .TryRun(
                (r) => receiver,
                (ex) => {},
                defaultValue: null)
            .Timeout(
                timeoutMillis,
                defaultValue: null,
                token);
    }
}
