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
        Func<IChordEndpoint, bool> ping = (e) => pingEndpoint(sender, e, pingTimeoutMillis);
        var allEndpoints = endpointGenerator.GenerateEndpoints();

        foreach (var endpointsToPing in allEndpoints.Chunk(numParallelPings))
        {
            var pingTasksByEndpoint = endpointsToPing
                .ToDictionary(e => e, e => Task.Run(() => ping(e)));

            while (pingTasksByEndpoint.Values.Any(x => x.Status == TaskStatus.Running))
            {
                await Task.WhenAny(pingTasksByEndpoint.Values);
                var bootstrapNode = pingTasksByEndpoint
                    .Where(x => x.Value.Status == TaskStatus.RanToCompletion && x.Value.Result)
                    .Select(x => x.Key)
                    .FirstOrDefault();

                if (bootstrapNode != null)
                   return bootstrapNode;
            }
        }

        return null;
    }

    private bool pingEndpoint(
        IChordClient sender,
        IChordEndpoint receiver,
        int timeoutMillis)
    {
        var tokenSource = new CancellationTokenSource();
        var requestTask = sender.SendRequest(
                new ChordRequestMessage() { Type = ChordRequestType.HealthCheck },
                receiver,
                tokenSource.Token);

        var timeoutTask = Task.Delay(timeoutMillis);
        bool ranIntoTimeout = Task.WaitAny(timeoutTask, requestTask) == 0;

        if (ranIntoTimeout)
            tokenSource.Cancel();

        return !ranIntoTimeout && requestTask.Status == TaskStatus.RanToCompletion;
    }
}
