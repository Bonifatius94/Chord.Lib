using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Chord.Lib.Test;

class LoggerAdapter : ILogger
{
    public LoggerAdapter(ITestOutputHelper logger)
        => this.logger = logger;

    private ITestOutputHelper logger;

    // info: this is just a dummy to make the interface work
    public IDisposable BeginScope<TState>(TState state)
        => new StreamWriter(new MemoryStream());

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        => logger.WriteLine($"{DateTime.UtcNow} {logLevel}: {formatter(state, exception)}");
}

class IPv4NetworkMock : IChordRequestProcessor
{
    // info: this is a list instead of a dict on purpose as
    //       the NodeId might change during the node init
    public List<ChordNode> Nodes { get; set; }
        = new List<ChordNode>();

    private HashSet<ChordRequestType> interceptTypes =
        new HashSet<ChordRequestType>() {
            ChordRequestType.KeyLookup,
            ChordRequestType.UpdateSuccessor,
            ChordRequestType.InitNodeJoin,
            ChordRequestType.CommitNodeJoin
        };

    public async Task<IChordResponseMessage> ProcessRequest(
        IChordRequestMessage request,
        CancellationToken token)
    {
        // look up the node that's receiving the request
        var receiver = request.Receiver;
        var receivingNode = Nodes
            .Where(x => x.NodeId == receiver.NodeId)
            .FirstOrDefault();

        // simulate a network timeout because the node doesn't exist
        if (receivingNode == null)
        {
            await Task.Delay(1000);
            throw new HttpRequestException(
                $"Endpoint with id {receiver.NodeId} not found!");
        }

        // TODO: think of adding random failures

        // simulate a short delay, then process the request
        await Task.Delay(5);
        return await receivingNode.ProcessRequest(request, token);
    }
}