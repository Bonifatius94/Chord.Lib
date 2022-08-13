using System.Collections;
using ChordConsumer = System.Action<Chord.Lib.ChordEvent>;

namespace Chord.Lib;

public enum ChordEventType
{
    Incoming,
    Outgoing
}

public class ChordEvent
{
    public ChordEvent(IChordRequestMessage message, ChordEventType direction)
    {
        Message = message;
        Direction = direction;
    }

    public IChordRequestMessage Message { get; private set; }
    public ChordEventType Direction { get; private set; }
}

public class ChordEventProcessor
{
    public ChordEventProcessor(
        ChordEventInputStream inputStream,
        ChordConsumer incomingMessageConsumer,
        ChordConsumer outgoingMessageConsumer)
    {
        this.eventInputStream = inputStream;
        this.incomingMessageConsumer = incomingMessageConsumer;
        this.outgoingMessageConsumer = outgoingMessageConsumer;
    }

    private readonly ChordEventInputStream eventInputStream;
    private readonly ChordConsumer incomingMessageConsumer;
    private readonly ChordConsumer outgoingMessageConsumer;

    public async Task ProcessQueueAsync(CancellationToken token)
    {
        Action<ChordEvent> consumeEvent = (e) => {
            if (e.Direction == ChordEventType.Incoming)
                incomingMessageConsumer(e);
            else if (e.Direction == ChordEventType.Outgoing)
                outgoingMessageConsumer(e);
        };

        Action processQueue = () => {
            foreach (var e in eventInputStream)
                consumeEvent(e);
        };

        await Task.Run(processQueue, token);
    }
}

public interface IChordEventSource
{
    bool HasNext();
    ChordEvent Next();
}

public class ChordEventInputStream : IEnumerable<ChordEvent>
{
    public ChordEventInputStream(List<IChordEventSource> producers)
        => this.producers = producers;

    private List<IChordEventSource> producers;

    public void RegisterProducer(IChordEventSource producer)
        => producers.Add(producer);

    public IEnumerator<ChordEvent> GetEnumerator()
    {
        while (true)
        {
            // pull from all producers in a Round-Robin manner
            var nextBatch = producers
                .Where(p => p.HasNext())
                .Select(p => p.Next());

            // avoid 100% CPU load if there's no incoming messages
            if (!nextBatch.Any())
                Task.Delay(1).Wait();

            // send the messages to the output
            foreach (var e in nextBatch)
                yield return e;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
        => this.GetEnumerator();
}
