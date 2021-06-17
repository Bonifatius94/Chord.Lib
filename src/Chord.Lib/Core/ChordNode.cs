using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Lib.Core
{
    // shortcut for message callback function: (message, receiver) -> response
    using MessageCallback = System.Func<IChordRequestMessage, IChordRemoteNode, Task<IChordResponseMessage>>;

    public class ChordNode
    {
        /// <summary>
        /// Create a new chord node with the given request callback and upper resource id bound.
        /// </summary>
        /// <param name="sendRequest">The callback function for sending requests to other chord nodes.</param>
        /// <param name="maxId">The max. resource id to be addressed (default: 2^63-1).</param>
        /// <param name="monitorHealthSchedule">The delay in seconds between health monitoring tasks (default: 5 minutes).</param>
        /// <param name="updateTableSchedule">The delay in seconds between finger table update tasks (default: 5 minutes).</param>
        public ChordNode(MessageCallback sendRequest, long maxId=long.MaxValue,
            int monitorHealthSchedule=600, int updateTableSchedule=600)
        {
            this.sendRequest = sendRequest;
            this.maxId = maxId;
        }

        private MessageCallback sendRequest;
        private long maxId;
        private int monitorHealthSchedule;
        private int updateTableSchedule;

        private long nodeId;
        private IChordRemoteNode successor = null;
        private IChordRemoteNode predecessor = null;

        private IDictionary<long, IChordRemoteNode> fingerTable
            = new Dictionary<long, IChordRemoteNode>();

        private CancellationTokenSource backgroundTaskCallback;

        #region Chord Client

        public async Task JoinNetwork(IChordRemoteNode bootstrap)
        {
            // phase 1: determine the successor by a key lookup

            do {

                nodeId = getRandId();
                successor = await LookupKey(nodeId, bootstrap);

            // continue until the generated node id is unique
            } while (successor.NodeId == nodeId);

            // phase 2: initiate the join process

            // send a join initiation request to the successor
            var response = await sendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.InitNodeJoin,
                    RequesterId = nodeId
                },
                successor
            );

            if (!response.ReadyForDataCopy) {
                throw new InvalidOperationException("Network join failed! Cannot copy payload data!"); }

            // phase 3: copy all existing payload data this node is now responsible for

            // TODO: For production use, make sure to copy payload data, too.
            //       Keep in mind that the old successor is no more responsible for the ids
            //       being assigned to this newly created node. So there needs to be a kind of
            //       mechanism to copy the data first before enabling this node to avoid
            //       temporary data unavailability.

            // phase 4: finalize the join process

            // send a join initiation request to the successor
            // -> the successor sends an 'update successor' request to his predecessor
            // -> the predecessor's successor then points to this node
            // -> from then on this node is available to other Chord nodes on the network
            response = await sendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.CommitNodeJoin,
                    RequesterId = nodeId
                },
                successor
            );

            // apply the node settings
            predecessor = response.Predecessor;
            fingerTable = response.FingerTable.ToDictionary(x => x.NodeId);
            fingerTable.Add(successor.NodeId, successor);
            // -> the node is now ready for use

            // start the health monitoring / finger table update
            // procedures as scheduled background tasks
            backgroundTaskCallback = new CancellationTokenSource();
            createBackgroundTasks(backgroundTaskCallback.Token);
        }

        private void createBackgroundTasks(CancellationToken token)
        {
            // define a task running the background tasks
            Task.Run(() => {

                // run the 'monitor health' task on a regular time schedule
                var monitorTask = Task.Run(() => {

                    while (!token.IsCancellationRequested)
                    {
                        monitorFingerHealth(token);
                        Task.Delay(monitorHealthSchedule * 1000).Wait();
                    }
                });

                // run the 'update table' task on a regular time schedule
                var updateTableTask = Task.Run(() => {

                    while (!token.IsCancellationRequested)
                    {
                        updateFingerTable(token).Wait();
                        Task.Delay(updateTableSchedule * 1000).Wait();
                    }
                });

                // wait until both tasks exited gracefully by cancellation
                Task.WaitAll(new Task[] { monitorTask, updateTableTask });
            });
        }

        public async Task LeaveNetwork()
        {
            // TODO: implement logic
            throw new NotImplementedException();
        }

        public async Task<IChordRemoteNode> LookupKey(
            long key, IChordRemoteNode explicitReceiver=null)
        {
            // determine the receiver to be forwarded the lookup request to
            var receiver = explicitReceiver ?? fingerTable[getBestFingerId(key)];

            // send a key lookup request
            var response = await sendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.KeyLookup,
                    RequesterId = nodeId,
                    RequestedResourceId = key
                },
                receiver
            );

            // return the node responsible for the key
            return response.Responder;
        }

        public async Task<ChordHealthStatus> CheckHealth(
            IChordRemoteNode target, int timeoutInSecs=10,
            ChordHealthStatus failStatus=ChordHealthStatus.Questionable)
        {
            // send a health check request
            var cancelCallback = new CancellationTokenSource();
            var timeoutTask = Task.Delay(timeoutInSecs * 1000);
            var healthCheckTask = Task.Run(() => sendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.HealthCheck,
                    RequesterId = nodeId,
                },
                target
            ), cancelCallback.Token);

            // return the reported health state or the fail status (timeout)
            bool timeout = await Task.WhenAny(timeoutTask, healthCheckTask) == timeoutTask;
            if (timeout) { cancelCallback.Cancel(); }
            return timeout ? failStatus : healthCheckTask.Result.Responder.State;

            // TODO: think about killing dangling requests
        }

        private void monitorFingerHealth(CancellationToken token)
        {
            // TODO: enter critical section (mutex)

            const int healthCheckTimeout = 10; // TODO: parameterize the delay by configuration
            var cachedFingers = fingerTable.Values.ToList();

            // perform a first health check for all finger nodes
            updateHealth(cachedFingers, healthCheckTimeout, ChordHealthStatus.Questionable);
            if (token.IsCancellationRequested) { return; } // TODO: release mutex

            // perform a second health check for all questionable finger nodes
            var questionableFingers = cachedFingers
                .Where(x => x.State == ChordHealthStatus.Questionable).ToList();
            updateHealth(questionableFingers, healthCheckTimeout / 2, ChordHealthStatus.Dead);
            if (token.IsCancellationRequested) { return; } // TODO: release mutex

            // TODO: exit critical section (mutex)
        }

        private void updateHealth(List<IChordRemoteNode> fingers, int timeoutSecs, 
            ChordHealthStatus failStatus=ChordHealthStatus.Questionable)
        {
            // run health checks for each finger in parallel
            var healthCheckTasks = fingerTable.Values
                .Select(x => Task.Run(() => new { 
                    NodeId = x.NodeId,
                    Health=CheckHealth(x, timeoutSecs, failStatus).Result
                })).ToArray();
            Task.WaitAll(healthCheckTasks);

            // collect health check results
            var healthStates = healthCheckTasks.Select(x => x.Result)
                .ToDictionary(x => x.NodeId, x => x.Health);

            // update finger states
            foreach (var finger in fingers)
            {
                finger.State = healthStates[finger.NodeId];
            }
        }

        private async Task updateFingerTable(CancellationToken token)
        {
            // TODO: add a mutex to protect this entrire critical section

            // get the ids 2^i for i in { 0, ..., log2(maxId) - 1 } to be looked up
            var keys = Enumerable.Range(0, (int)Math.Log2(maxId) - 1)
                .Select(i => restMod((long)Math.Pow(i, 2) + nodeId, maxId))
                .Select(normKey => (long)restMod(normKey - nodeId, maxId));

            // run all lookup tasks in parallel
            var lookupTasks = keys.Select(x => LookupKey(x)).ToArray();
            Task.WaitAll(lookupTasks);

            // create a new finger table by assigning the nodes that
            // responded to the lookup requests (including the successor)
            var fingers = lookupTasks.Select(x => x.Result);
            var newTable = fingers.ToDictionary(x => x.NodeId);

            // switch out the currently active finger table
            fingerTable = newTable;
        }

        #endregion Chord Client

        #region Chord Server

        public async Task<IChordResponseMessage> ProcessRequest(IChordRequestMessage request)
        {
            switch (request.Type)
            {
                // key lookup, health check and update successor
                case ChordRequestType.KeyLookup:
                    return await processKeyLookup(request);
                case ChordRequestType.HealthCheck:
                    return await processHealthCheck(request);
                case ChordRequestType.UpdateSuccessor:
                    return await processUpdateSuccessor(request);

                // node join procedure (init/commit)
                case ChordRequestType.InitNodeJoin:
                    return await processInitNodeJoin(request);
                case ChordRequestType.CommitNodeJoin:
                    return await processCommitNodeJoin(request);

                // node leave procedure (init/commit)
                case ChordRequestType.InitNodeLeave:
                    return await processInitNodeLeave(request);
                case ChordRequestType.CommitNodeLeave:
                    return await processCommitNodeLeave(request);

                // handle unknown request types by throwing an error
                default: throw new ArgumentException(
                    $"Invalid argument! Unknown request type { request.Type }!");
            }
        }

        private async Task<IChordResponseMessage> processKeyLookup(
            IChordRequestMessage request)
        {
            // TODO: implement logic
            throw new NotImplementedException();
        }

        private async Task<IChordResponseMessage> processHealthCheck(
            IChordRequestMessage request)
        {
            // respond to a health check with the current node state
            // 

            // TODO: implement logic
            throw new NotImplementedException();
        }

        private async Task<IChordResponseMessage> processInitNodeJoin(
            IChordRequestMessage request)
        {
            // prepare for a joining node as new predecessor
            // inform the payload component that it has to send the payload
            // data chunk to the joining node that it is now responsible for

            // TODO: implement logic
            throw new NotImplementedException();
        }

        private async Task<IChordResponseMessage> processCommitNodeJoin(
            IChordRequestMessage request)
        {
            // request the prodecessor's successor to be updated to the joining node
            // -> predecessor.successor = joining node
            // -> this.predecessor = joining node

            // TODO: implement logic
            throw new NotImplementedException();
        }

        private async Task<IChordResponseMessage> processInitNodeLeave(
            IChordRequestMessage request)
        {
            // prepare for the predecessor leaving the network
            // inform the payload component that it will be sent payload data

            // TODO: implement logic
            throw new NotImplementedException();
        }

        private async Task<IChordResponseMessage> processCommitNodeLeave(
            IChordRequestMessage request)
        {
            // request updating the leaving node's prodecessor's successor
            // -> predecessor.successor = this node

            // TODO: implement logic
            throw new NotImplementedException();
        }

        private async Task<IChordResponseMessage> processUpdateSuccessor(
            IChordRequestMessage request)
        {
            // ping the new successor by sending a health check
            // if the new successor's health is fine, then update the successor

            // TODO: implement logic
            throw new NotImplementedException();
        }

        #endregion Chord Server

        #region Common Code

        private long getBestFingerId(long key)
        {
            // cache the finger table ids to ensure operation consistency
            var cachedFingerIds = fingerTable.Keys.ToList();

            // make sure the finger table is not empty
            if (cachedFingerIds.Count == 0) { throw new InvalidOperationException(
                "The finger table is empty! Make sure the node was properly initialized!"); }

            // termination case: forward to the successor if it is the manager of the key
            // recursion case: forward to the closest predecessing finger of the key
            return successor.NodeId >= key ? successor.NodeId
                : getClosestPredecessor(key, cachedFingerIds);
        }

        private long getClosestPredecessor(long key, IEnumerable<long> fingerIds)
        {
            // shift the finger ids such that the key's modulo identity becomes 0
            // then select the biggest id, which has to be the closest predecessor
            BigInteger closestNormPred = fingerIds
                .Select(x => restMod(x + key, maxId)).Max();

            // convert the shifted node id back to the actual node id
            return (long)restMod(closestNormPred - key, maxId);
        }

        #endregion Common Code

        #region Helpers

        // initialize the random number generator
        private static readonly Random rng = new Random();

        private long getRandId()
        {
            // concatenate two random 32-bit integers to a long value
            return ((long)rng.Next(int.MinValue, int.MaxValue) << 32)
                 | (long)(uint)rng.Next(int.MinValue, int.MaxValue);
        }

        // handles all negative rest classes correctly by mapping
        // them to their positive identity within [0, classes-1]
        private BigInteger restMod(BigInteger element, BigInteger classes)
            => (element % classes + classes) % classes;

        #endregion Helpers
    }
}