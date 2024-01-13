namespace Chord.Lib;

public class ChordBootstrapper : IChordBootstrapper
{
    public ChordBootstrapper(
        IEnumerable<IChordEndpoint> endpointGenerator,
        int pingTimeoutMillis = 1000,
        int numParallelPings = 128)
    {
        this.endpointGenerator = endpointGenerator;
        this.pingTimeoutMillis = pingTimeoutMillis;
        this.numParallelPings = numParallelPings;
    }

    private readonly IEnumerable<IChordEndpoint> endpointGenerator;
    private readonly int pingTimeoutMillis;
    private readonly int numParallelPings;

    private bool isSuccessState(ChordHealthStatus state)
        => state == ChordHealthStatus.Starting || state == ChordHealthStatus.Idle;

    public async Task<IChordEndpoint> FindBootstrapNode(
        ChordRequestSender sender, IChordEndpoint local)
    {
        var cancelCallback = new CancellationTokenSource();
        var ping = async (IChordEndpoint receiver) => {
                var state = await sender.HealthCheck(
                    local, receiver, cancelCallback.Token,
                    timeoutInMillis: pingTimeoutMillis);
                return isSuccessState(state) ? receiver : null;
            };

        // TODO: don't let local node ping itself

        foreach (var endpointsToPing in endpointGenerator.Chunk(numParallelPings))
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
