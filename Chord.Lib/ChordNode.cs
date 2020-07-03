using Chord.Lib.Protocol;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Lib
{
    /// <summary>
    /// An implementation of a P2P node running the chord protocol.
    /// </summary>
    public class ChordNode
    {
        #region Constructor

        /// <summary>
        /// Initialize a new chord node with the given IP settings.
        /// </summary>
        /// <param name="localEndpoint">The local IP endpoint configuration of the node.</param>
        /// <param name="logger">The local IP endpoint configuration of the node.</param>
        public ChordNode(IPEndPoint localEndpoint, ILogger logger)
        {
            // apply logger
            _logger = logger;

            // apply IP settings
            Local = localEndpoint;

            // compute the node's hash
            var hash = HashingHelper.GetSha1Hash(Local);
            NodeId = new BigInteger(hash);
        }

        #endregion Constructor

        #region Members

        /// <summary>
        /// The logger handle for writing to the console output.
        /// </summary>
        private ILogger _logger;

        private ChordClient _client;

        private ChordServer _server;
        private CancellationTokenSource _serverListenerCancel;

        /// <summary>
        /// The chord node's local IP endpoint configuration.
        /// </summary>
        public IPEndPoint Local { get; private set; }

        /// <summary>
        /// The chord node's SHA-1 hash identificator.
        /// </summary>
        public BigInteger NodeId { get; private set; }

        /// <summary>
        /// The chord node's successors.
        /// </summary>
        public IList<ChordRemoteNode> Successors { get; private set; } = new List<ChordRemoteNode>();

        /// <summary>
        /// The chord node's predecessor.
        /// </summary>
        public ChordRemoteNode Predecessor { get; private set; }

        /// <summary>
        /// The chord node's finger table SHA-1 hash identificators.
        /// </summary>
        public IList<ChordRemoteNode> FingerTableEntries { get; private set; } = new List<ChordRemoteNode>();

        /// <summary>
        /// The chord node's direct successor.
        /// </summary>
        public ChordRemoteNode Successor => Successors.FirstOrDefault();

        #endregion Members

        #region Methods

        /// <summary>
        /// Perform a lookup request to retrieve the node managing the given hash key.
        /// </summary>
        /// <param name="key">The hash identifier to be looked up.</param>
        /// <returns>the node id of the managing node</returns>
        public async Task<BigInteger> LookupKey(BigInteger key)
        {
            BigInteger nodeId;

            // make sure that at least one successor is defined (for simple token-ring routing)
            if (Successor == null) { throw new InvalidOperationException($"Node is not initialized properly! Please run { nameof(JoinNetwork) } function first!"); }

            // check if the direct successor manages the hash
            if (Successor.NodeId > key)
            {
                nodeId = Successor.NodeId;
            }
            // foreward the request
            else
            {
                // select best finger
                var bestFinger = FingerTableEntries.Where(x => x.NodeId < key).OrderByDescending(x => x.NodeId).FirstOrDefault();
                nodeId = await _client.LookupKey(Local, bestFinger.Endpoint, key);
            }

            return nodeId;
        }

        /// <summary>
        /// Find an entrance into the P2P network by bootstrapping (brute-force).
        /// </summary>
        /// <param name="networkId">The network id.</param>
        /// <param name="broadcast">The network broadcast.</param>
        /// <returns>the endpoint</returns>
        public async Task<IPEndPoint> FindBootstrapNode(IPAddress networkId, IPAddress broadcast)
        {
            // init min / max address for brute-force search range
            int min = BitConverter.ToInt32(networkId.GetAddressBytes(), 0) + 1;
            int max = BitConverter.ToInt32(broadcast.GetAddressBytes(), 0);

            // loop through all possible peers
            for (int address = min; address < max; address++)
            {
                // put potential endpoint together
                var endpoint = new IPEndPoint(new IPAddress(address), Local.Port);

                // skip own IP address
                if (new BigInteger(Local.Address.GetAddressBytes()) == new BigInteger(endpoint.Address.GetAddressBytes())) { continue; }

                // run lookup and 3 sec timeout task
                var lookupTask = _client.LookupKey(Local, endpoint, NodeId);
                var timeoutTask = Task.Delay(3000);

                // only terminate if the lookup task finishes before the timeout task
                if (await Task.WhenAny(lookupTask, timeoutTask) == lookupTask) { return endpoint; }
                else { lookupTask.Dispose(); }

                // TODO: implement greedy lookup trying multiple IP addresses at once
            }

            // default return value when all IP addresses 
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bootstrapNode"></param>
        public async Task JoinNetwork(IPEndPoint bootstrapNode)
        {
            // find successor and predecessor


            // start listening to incoming messages
            _serverListenerCancel = new CancellationTokenSource();
            _server.ListenMessages(Local, handleIncomingMessage, _serverListenerCancel.Token);
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task LeaveNetwork()
        {
            // reject all new incoming messages
            _serverListenerCancel.

            // finish all running tasks


            // stop listening to incoming messages
            _listenIncomingMessages.Dispose();
        }

        private void handleIncomingMessage(IChordMessage message)
        {

        }

        #endregion Methods
    }
}
