using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Chord.Lib.Impl;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Chord.Lib.Test;

class LoggerAdapter : ILogger
{
    public LoggerAdapter(ITestOutputHelper logger)
        => this.logger = logger;

    private ITestOutputHelper logger;

    public IDisposable BeginScope<TState>(TState state)
    {
        // this is just a dummy to make the interface work
        return new StreamWriter(new MemoryStream());
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        var logText = formatter(state, exception);
        logger.WriteLine($"{DateTime.UtcNow} {logLevel}: {logText}");
    }
}

class IPv4NetworkMock : IChordClient
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

    public async Task<IChordResponseMessage> SendRequest(
        IChordRequestMessage request,
        IChordEndpoint receiver,
        CancellationToken? token = null)
    {
        // look up the node that's receiving the request
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
        return await receivingNode.ProcessRequest(request);
    }
}

public class ChordNetworkSimulationTest
{
    private readonly ITestOutputHelper _logger;
    public ChordNetworkSimulationTest(ITestOutputHelper logger)
    {
        _logger = logger;
        var adaptedLogger = new LoggerAdapter(_logger);
        adaptedLogger.LogInformation("some important info xyz");
    }

    [Fact(Skip="not ready yet")]
    public async Task SimulateNetwork()
    {
        // define test hyperparams
        const int testNodesCount = 100;
        const int keySpace = 100000;
        const int chordPort = 9876;
        const int testTimeoutSecs = 5 * 60;

        _logger.WriteLine($"Simulating a chord network with { testNodesCount } nodes, timeout={ testTimeoutSecs }s");

        var endpoints = Enumerable.Range(1, testNodesCount)
                .Select(x => new IPv4Endpoint(
                    ChordKey.PickRandom(keySpace),
                    ChordHealthStatus.Starting,
                    $"10.0.0.{ x }",
                    chordPort.ToString()))
                .ToList();

        var networkClient = new IPv4NetworkMock();
        var nodes = endpoints
            .Select(local =>
                new ChordNode(
                    local,
                    networkClient,
                    new ZeroProtocolPayloadWorker(),
                    new ChordNodeConfiguration(),
                    new LoggerAdapter(_logger)))
            .ToList();
        networkClient.Nodes = nodes;

        // init network simulation stuff
        var bootstrapNode = endpoints.First<IChordEndpoint>();
        var bootstrapperMock = Substitute.For<IChordBootstrapper>();
        bootstrapperMock.FindBootstrapNode(default, default)
            .ReturnsForAnyArgs(x => Task.FromResult(
                endpoints.Except(new List<IChordEndpoint> { x[1] as IChordEndpoint }).First()));

        _logger.WriteLine("Successfully created nodes. Starting node join procedures.");

        // connect the chord nodes to a self-organized cluster by simulating
        // something like e.g. a Kubernetes rollout of several chord instances
        // var joinTasks = nodesWithEndpoints
        //     .Select(x => Task.Run(() => x.First.JoinNetwork(x.Second, bootstrapperMock)))
        //     .ToArray();

        try {
            var retryTimeouts = new int[] { 100, 500, 2000 };
            var exHandler = (Exception ex) => _logger.WriteLine(ex.ToString());

            var join1Factory = () => nodes[0].JoinNetwork(bootstrapperMock);
            var join1 = join1Factory.TryRepeat(retryTimeouts, exHandler);

            var join2Factory = () => nodes[1].JoinNetwork(bootstrapperMock);
            var join2 = join2Factory.TryRepeat(retryTimeouts, exHandler);

            await Task.WhenAll(join1, join2);
        } catch (Exception ex) {
            Console.WriteLine(ex.StackTrace);
        }

        nodes[0].NodeState.Should().Be(ChordHealthStatus.Idle);
        nodes[1].NodeState.Should().Be(ChordHealthStatus.Idle);

        // // log the system state on a regular schedule until all join tasks completed
        // // abort after several minutes if the tasks did not finish until then -> unit test failed
        // var cancelCallback = new CancellationTokenSource();
        // var timeoutTask = Task.Delay(testTimeoutSecs * 1000);
        // var monitorTask = Task.Run(() => {

        //         int i = 0;
        //         while (joinTasks.Any(x => x.Status == TaskStatus.Running))
        //         {
        //             // report the states on a 5 second schedule
        //             Task.Delay(5000).Wait();

        //             // log the episode's system status
        //             _logger.WriteLine("==================================");
        //             _logger.WriteLine($"System state after { ++i } seconds:");
        //             _logger.WriteLine(string.Join("\n", joinTasks.Select(task => $"task { task.Id }: { task.Status }")));
        //         }

        //         Task.WaitAll(joinTasks);

        //     }, cancelCallback.Token);

        // // abort the simulation on timeout if needed -> unit test failed
        // bool allTasksComplete = Task.WhenAny(timeoutTask, monitorTask) != timeoutTask;
        // Assert.True(allTasksComplete);

        // _logger.WriteLine("Successfully joined all nodes to the chord network.");

        // TODO: evaluate the network structure by some graph analysis

        // TODO: test sending some key lookups and health checks

        // TODO: test the node leave prodecure (in particular the case for the last leaving node)
    }
}
