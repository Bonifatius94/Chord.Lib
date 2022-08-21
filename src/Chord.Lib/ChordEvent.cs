namespace Chord.Lib;

public enum ChordEventType
{
    Incoming,
    Outgoing
}

public class ChordEvent
{
    internal ChordEvent(
        IChordRequestMessage request,
        IChordRequestProcessor processor)
    {
        Request = request;
        this.processor = processor;
    }

    private readonly IChordRequestProcessor processor;
    public IChordRequestMessage Request { get; private set; }
    public ChordEventType Direction { get; private set; } = ChordEventType.Incoming;
    public bool IsProcessingComplete { get; private set; } = false;
    public IChordResponseMessage Response { get; private set; } = null;

    public async Task<IChordResponseMessage> OnProcessingComplete(
        CancellationToken token)
    {
        while (!IsProcessingComplete && !token.IsCancellationRequested)
            await Task.Delay(5);

        return Response;
    }

    public async Task Process(CancellationToken token)
    {
        Response = await processor.ProcessRequest(Request, token);
        IsProcessingComplete = true;
    }
}
