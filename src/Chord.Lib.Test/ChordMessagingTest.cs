// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Net.Http;
// using System.Numerics;
// using System.Threading;
// using System.Threading.Tasks;
// using Chord.Lib.Impl;
// using FluentAssertions;
// using Microsoft.Extensions.Logging;
// using NSubstitute;
// using Xunit;

// namespace Chord.Lib.Test.MessagingTests;

// class IPv4NetworkMock : IChordClient
// {
//     // info: this is a list instead of a dict on purpose as
//     //       the NodeId might change during the node init
//     public List<ChordNode> Nodes { get; set; }
//         = new List<ChordNode>();

//     public bool IsInitPhase { get; set; } = true;

//     private HashSet<ChordRequestType> interceptTypes =
//         new HashSet<ChordRequestType>() {
//             ChordRequestType.KeyLookup,
//             ChordRequestType.UpdateSuccessor,
//             ChordRequestType.InitNodeJoin,
//             ChordRequestType.CommitNodeJoin
//         };

//     public async Task<IChordResponseMessage> SendRequest(
//         IChordRequestMessage request,
//         IChordEndpoint receiver,
//         CancellationToken? token = null)
//     {
//         // look up the node that's receiving the request
//         var receivingNode = Nodes
//             .Where(x => x.NodeId == receiver.NodeId)
//             .FirstOrDefault();

//         // simulate a network timeout because the node doesn't exist
//         if (receivingNode == null)
//         {
//             await Task.Delay(1000);
//             throw new HttpRequestException(
//                 $"Endpoint with id {receiver.NodeId} not found!");
//         }

//         // TODO: think of adding random failures

//         // simulate a short delay, then process the request
//         await Task.Delay(5);
//         return await receivingNode.ProcessRequest(request);
//     }
// }

// public class ForwardingTest
// {
//     #region Init

//     public ForwardingTest(ILogger logger)
//     {
//         this.logger = logger;
//     }

//     private ILogger logger;

//     private List<ChordNode> initNetworkNodes(
//         IList<IChordEndpoint> endpoints)
//     {
//         var endpointsInOrderOfNode = Enumerable.Range(0, endpoints.Count)
//             .Select(i => endpoints.ShiftRingBy(i));
//         var fingerTablesByNodeId = endpointsInOrderOfNode
//             .ToDictionary(
//                 endpoints => endpoints.First().NodeId,
//                 endpoints => initFingerTable(endpoints.ToList()));

//         var nodeConfig = new ChordNodeConfiguration() {
//             HealthCheckTimeoutMillis = 10,
//             MonitorHealthSchedule = 10000,
//             UpdateTableSchedule = 10000,
//         };
//         var client = new IPv4NetworkMock();
//         var worker = new ZeroProtocolPayloadWorker();

//         var node = new ChordNode();
//     }

//     private ChordFingerTable initFingerTable(
//         IList<IChordEndpoint> endpoints)
//     {
//         var local = endpoints[0];
//         var successor = endpoints[1];
//         var predecessor = endpoints.Last();

//         var fingerTable = new ChordFingerTable(
//             (k, t) => Task.FromResult(
//                 endpoints.MinBy(x => x.NodeId - k)),
//             local,
//             successor);

//         fingerTable.BuildTable().Wait();
//         return fingerTable;
//     }

//     const int KeySpace = 1000000;

//     Func<BigInteger, IChordEndpoint> endpointOfKey = (k) => new IPv4Endpoint(
//             new ChordKey(k, KeySpace), ChordHealthStatus.Idle, null, null);

//     #endregion Init

//     [Fact]
//     public async Task Test_ShouldForwardMessage_WhenProcessingNodeIsNotTheReceiver()
//     {
//         // TODO: test this with a couple of initialized nodes
//     }
// }

// public static class RingBufferEx
// {
//     public static IEnumerable<T> ShiftRingBy<T>(this IEnumerable<T> items, int shift)
//         => items.Skip(shift).Concat(items.Take(shift));
// }
