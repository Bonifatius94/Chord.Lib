namespace Chord.Lib;

public class ChordBootstrapper : IChordBootstrapper
{
    public ChordBootstrapper(
        IExplorableChordEndpointGenerator endpointGenerator,
        int pingTimeoutMillis = 1000,
        int numParallelPings = 128)
    {
        this.endpointGenerator = endpointGenerator;
        this.pingTimeoutMillis = pingTimeoutMillis;
        this.numParallelPings = numParallelPings;
    }

    private readonly IExplorableChordEndpointGenerator endpointGenerator;
    private readonly int pingTimeoutMillis;
    private readonly int numParallelPings;

    private static readonly HashSet<ChordHealthStatus> successStates =
        new HashSet<ChordHealthStatus>() {
            ChordHealthStatus.Starting,
            ChordHealthStatus.Idle
        };

    public async Task<IChordEndpoint> FindBootstrapNode(
        ChordRequestSender sender, IChordEndpoint local)
    {
        var cancelCallback = new CancellationTokenSource();
        var ping = async (IChordEndpoint receiver) => {
                var state = await sender.HealthCheck(
                    local,
                    receiver,
                    timeoutInMillis: pingTimeoutMillis,
                    token: cancelCallback.Token);
                return successStates.Contains(state) ? receiver : null;
            };

        var allEndpoints = endpointGenerator.GenerateEndpoints();
        // TODO: don't let local node ping itself

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
}
