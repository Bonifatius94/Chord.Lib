namespace Chord.Lib;

public class ChordEventProcessor
{
    public ChordEventProcessor(SynchronizedChordMessageBus messageBus)
        => this.messageBus = messageBus;

    private readonly SynchronizedChordMessageBus messageBus;

    private static readonly ISet<ChordRequestType> readonlyRequests =
        new HashSet<ChordRequestType>() {
            ChordRequestType.KeyLookup,
            ChordRequestType.HealthCheck,
        };

    public async Task StartProcessingEventsAsDaemon(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            while (messageBus.HasEvents() && !token.IsCancellationRequested)
            {
                var chordEvent = messageBus.Dequeue();
                await chordEvent.Process(token);
            }

            await Task.Delay(5);
        }
    }
}

public class SynchronizedChordMessageBus
{
    private ConcurrentQueue<ChordEvent> queue =
        new ConcurrentQueue<ChordEvent>();

    public void Enqueue(ChordEvent e) => queue.Enqueue(e);

    public bool HasEvents() => queue.Any();

    public ChordEvent Dequeue()
    {
        ChordEvent e = null;
        queue.TryDequeue(out e);
        return e;
    }
}

public class SynchronizedRequestProcessorProxy : IChordRequestProcessor
{
    public SynchronizedRequestProcessorProxy(
            SynchronizedChordMessageBus messageBus,
            IChordRequestProcessor onlineClient)
    {
        this.messageBus = messageBus;
        this.onlineClient = onlineClient;
    }

    private readonly SynchronizedChordMessageBus messageBus;
    private readonly IChordRequestProcessor onlineClient;

    public async Task<IChordResponseMessage> ProcessRequest(
        IChordRequestMessage request,
        CancellationToken token)
    {
        var chordEvent = new ChordEvent(request, onlineClient);
        messageBus.Enqueue(chordEvent);
        return await chordEvent.OnProcessingComplete(token);
    }
}
