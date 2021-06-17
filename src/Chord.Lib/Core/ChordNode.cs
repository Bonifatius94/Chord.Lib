using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Chord.Lib.Core
{
    // shortcut for message callback function
    using MessageCallback = System.Func<IChordRequestMessage, Task<IChordResponseMessage>>;

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

        public async Task JoinNetwork()
        {
            // phase 1: determine the successor by a key lookup

            do {

                nodeId = getRandId();
                successor = await LookupKey(nodeId);

            // continue until the generated node id is unique
            } while (successor.NodeId == nodeId);

            // phase 2: initiate the join process and apply the node settings

            // send a join request to the successor
            var response = await sendRequest(new ChordRequestMessage() {
                Type = ChordRequestType.InitNodeJoin,
                RequesterId = nodeId
            });

            // apply the node settings
            predecessor = response.Predecessor;
            fingerTable = response.FingerTable.ToDictionary(x => x.NodeId);

            // the node is now ready for use

            // TODO: For production use, make sure to copy payload data, too.
            //       Keep in mind that the old successor is no more responsible for the ids
            //       being assigned to this newly created node. So there needs to be a kind of
            //       mechanism to copy the data first before enabling this node to avoid
            //       temporary data unavailability.
            //       -> think of adding another phase to the join protocol, just for copying data
        }

        public async Task LeaveNetwork()
        {
            // TODO: implement logic
            throw new NotImplementedException();
        }

        public async Task<IChordRemoteNode> LookupKey(long key)
        {
            // send a key lookup request
            var lookupResponse = await sendRequest(new ChordRequestMessage() {
                Type = ChordRequestType.KeyLookup,
                RequesterId = nodeId,
                RequestedResourceId = key
            });

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
            return ((long)rng.Next() << 32) | (long)(uint)rng.Next();
        }

        #endregion Helpers
    }
}