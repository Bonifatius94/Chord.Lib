namespace Chord.Lib;

public enum ChordEventType
{
    Incoming,
    Outgoing
}

public class ChordEvent
{
    public ChordEvent(IChordRequestMessage request, ChordEventType direction)
    {
        Request = request;
        Direction = direction;
    }

    public IChordRequestMessage Request { get; private set; }
    public ChordEventType Direction { get; private set; }
    public bool IsProcessingComplete { get; private set; } = false;
    public IChordResponseMessage Response { get; private set; }

    public async Task<IChordResponseMessage> OnProcessingComplete(CancellationToken token)
    {
        while (!IsProcessingComplete)
            await Task.Delay(5);

        return Response;
    }

    public void ProcessingCompleted(IChordResponseMessage response)
    {
        IsProcessingComplete = true;
        Response = response;
    }
}

public class ChordEventProcessor
{
    public ChordEventProcessor(ChordRequestReceiver receiver)
    {
        this.receiver = receiver;
    }

    private readonly ChordRequestReceiver receiver;

    private BigInteger id = 0;
    private readonly PriorityQueue<ChordEvent, BigInteger> queue =
        new PriorityQueue<ChordEvent, BigInteger>();
    private readonly Mutex canEnqueue = new Mutex();

    public void Enqueue(ChordEvent chordEvent)
    {
        canEnqueue.WaitOne();
        queue.Enqueue(chordEvent, id++);
        canEnqueue.ReleaseMutex();
    }

    private static readonly ISet<ChordRequestType> readonlyRequests =
        new HashSet<ChordRequestType>() {
            ChordRequestType.KeyLookup,
            ChordRequestType.HealthCheck,
        };

    public async Task ProcessEventsAsDaemon(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            BigInteger id;
            ChordEvent chordEvent;

            while (queue.TryDequeue(out chordEvent, out id))
            {
                // make sure 
                if (readonlyRequests.Contains(chordEvent.Request.Type))
                {

                }

                if (chordEvent.Direction == ChordEventType.Incoming)
                {
                    var response = await receiver.ProcessAsync(
                        chordEvent.Request, token);
                    chordEvent.ProcessingCompleted(response);
                }
                else
                {

                }
            }

            await Task.Delay(10);
        }
    }
}

// public class ChordEventProcessor
// {
//     public ChordEventProcessor(
//         ChordEventInputStream inputStream,
//         ChordConsumer incomingMessageConsumer,
//         ChordConsumer outgoingMessageConsumer)
//     {
//         this.eventInputStream = inputStream;
//         this.incomingMessageConsumer = incomingMessageConsumer;
//         this.outgoingMessageConsumer = outgoingMessageConsumer;
//     }

//     private readonly ChordEventInputStream eventInputStream;
//     private readonly ChordConsumer incomingMessageConsumer;
//     private readonly ChordConsumer outgoingMessageConsumer;

//     public async Task ProcessQueueAsync(CancellationToken token)
//     {
//         Action<ChordEvent> consumeEvent = (e) => {
//             if (e.Direction == ChordEventType.Incoming)
//                 incomingMessageConsumer(e);
//             else if (e.Direction == ChordEventType.Outgoing)
//                 outgoingMessageConsumer(e);
//         };

//         Action processQueue = () => {
//             foreach (var e in eventInputStream)
//                 consumeEvent(e);
//         };

//         await Task.Run(processQueue, token);
//     }
// }

// public interface IChordEventSource
// {
//     bool HasNext();
//     ChordEvent Next();
// }

// public class ChordEventInputStream : IEnumerable<ChordEvent>
// {
//     public ChordEventInputStream(List<IChordEventSource> producers)
//         => this.producers = producers;

//     private List<IChordEventSource> producers;

//     public void RegisterProducer(IChordEventSource producer)
//         => producers.Add(producer);

//     public IEnumerator<ChordEvent> GetEnumerator()
//     {
//         while (true)
//         {
//             // pull from all producers in a Round-Robin manner
//             var nextBatch = producers
//                 .Where(p => p.HasNext())
//                 .Select(p => p.Next());

//             // avoid 100% CPU load if there's no incoming messages
//             if (!nextBatch.Any())
//                 Task.Delay(1).Wait();

//             // send the messages to the output
//             foreach (var e in nextBatch)
//                 yield return e;
//         }
//     }

//     IEnumerator IEnumerable.GetEnumerator()
//         => this.GetEnumerator();
// }
