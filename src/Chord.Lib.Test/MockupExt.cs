using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib.Impl;
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

class VirtualIPv4Network : IChordRequestProcessor
{
    // info: This is a plain list instead of a dict on purpose.
    //       A dict with NodeId as key can fail because the
    //       NodeId might change during init procedure.
    public List<ChordNode> Nodes { get; set; }
        = new List<ChordNode>();

    public void RegisterNode(ChordNode node) => Nodes.Add(node);

    public void PopulateNetwork(int numNodes)
    {
        if (numNodes > 254) throw new ArgumentException(
            "Type C IPv4 network does not support more than 254 nodes");

        var config = new ChordNodeConfiguration() { UpdateTableSchedule = 1 };
        var worker = new ZeroProtocolPayloadWorker();
        Nodes = Enumerable.Range(1, numNodes)
            .Select(id => new IPv4Endpoint($"192.168.178.{id}", "5000", 254))
            .Select(e => new ChordNode(e, this, worker, config))
            .ToList();
    }

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
