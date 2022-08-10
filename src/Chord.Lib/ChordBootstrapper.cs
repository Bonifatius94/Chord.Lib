namespace Chord.Lib;

public class ChordBootstrapper : IChordBootstrapper
{
    public ChordBootstrapper(
        IChordClient sender,
        IExplorableChordEndpointGenerator endpointGenerator)
    {
        this.sender = sender;
        this.endpointGenerator = endpointGenerator;
    }

    private IChordClient sender;
    private IExplorableChordEndpointGenerator endpointGenerator;

    public async Task<IChordEndpoint> FindBootstrapNode()
    {
        // TODO: make sure the bootstrapper doesn't return the local endpoint as bootstrap node
        const int PING_TIMEOUT_MS = 1000;
        const int NUM_PARALLEL_PINGS = 128;

        Func<IChordEndpoint, bool> ping = (e) => pingEndpoint(sender, e, PING_TIMEOUT_MS);
        var allEndpoints = endpointGenerator.GenerateEndpoints();

        foreach (var endpointsToPing in allEndpoints.Chunk(NUM_PARALLEL_PINGS))
        {
            var pingTasksByEndpoint = endpointsToPing
                .ToDictionary(x => x, x => Task.Run(() => ping(x)));

            while (pingTasksByEndpoint.Values.Any(x => x.Status == TaskStatus.Running))
            {
                await Task.WhenAny(pingTasksByEndpoint.Values);
                var bootstrapNode = pingTasksByEndpoint
                    .Where(x => x.Value.Status == TaskStatus.RanToCompletion)
                    .FirstOrDefault(x => x.Value.Result).Key;

                if (bootstrapNode != null)
                   return bootstrapNode;
            }
        }

        return null;
    }

    private bool pingEndpoint(
        IChordClient sender,
        IChordEndpoint endpoint,
        int timeoutMillis)
    {
        Action ping = () => sender.SendRequest(
                new ChordRequestMessage() { Type = ChordRequestType.HealthCheck },
                endpoint
            );

        var tokenSource = new CancellationTokenSource();
        var requestTask = Task.Run(ping, tokenSource.Token);

        var timeoutTask = Task.Delay(timeoutMillis);
        bool ranIntoTimeout = Task.WaitAny(timeoutTask, requestTask) == 0;

        if (ranIntoTimeout)
            tokenSource.Cancel();

        return !ranIntoTimeout;
    }
}
