using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        public ChordNode(MessageCallback sendRequest, long maxId=long.MaxValue)
        {
            this.sendRequest = sendRequest;
            this.maxId = maxId;
        }

        private MessageCallback sendRequest;
        private long maxId;

        private long nodeId;
        private IChordRemoteNode successor = null;
        private IChordRemoteNode predecessor = null;

        private IDictionary<long, IChordRemoteNode> fingerTable
            = new Dictionary<long, IChordRemoteNode>();

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

            // the node is now ready for use
        }

        public async Task LeaveNetwork()
        {
            
        }

        public async Task<IChordRemoteNode> LookupKey(
            long key, IChordRemoteNode explicitReceiver=null)
        {
            // determine the receiver to be forwarded the lookup request
            long bestFingerId = fingerTable.Keys.Select(x => x - nodeId)
                .Where(x => x <= key - nodeId).Max() + nodeId;
            var receiver = explicitReceiver ?? fingerTable[bestFingerId];

            // send a key lookup request
            var lookupResponse = await sendRequest(
                new ChordRequestMessage() {
                    Type = ChordRequestType.KeyLookup,
                    RequesterId = nodeId,
                    RequestedResourceId = key
                },
                receiver
            );

            // return the node responsible for the key
            return lookupResponse.Responder;
        }

        public async Task ProcessRequest(IChordRequestMessage request)
        {
            // TODO: implement logic
            throw new NotImplementedException();
        }

        #region Helpers

        // initialize the random number generator
        private static readonly Random rng = new Random();

        private long getRandId()
        {
            // concatenate two random 32-bit integers to a long value
            return ((long)rng.Next(int.MinValue, int.MaxValue) << 32)
                 | (long)(uint)rng.Next(int.MinValue, int.MaxValue);
        }

        #endregion Helpers
    }
}