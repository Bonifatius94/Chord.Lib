using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Numerics;
using System.Text;

namespace Chord.Lib
{
    /// <summary>
    /// An implementation of a remote node connected by the chord protocol.
    /// </summary>
    public class ChordRemoteNode
    {
        #region Constructor

        /// <summary>
        /// Initialize a new chord node with the given node hash and IP endpoint configuration.
        /// </summary>
        /// <param name="nodeId">The remote node's hash identifier.</param>
        /// <param name="endpoint">The remote node's IP endpoint configuration.</param>
        public ChordRemoteNode(BigInteger nodeId, IPEndPoint endpoint)
        {
            NodeId = nodeId;
            Endpoint = endpoint;
        }

        #endregion Constructor

        #region Members

        /// <summary>
        /// The remote node's hash identifier.
        /// </summary>
        public BigInteger NodeId { get; private set; }

        /// <summary>
        /// The remote node's IP endpoint configuration.
        /// </summary>
        public IPEndPoint Endpoint { get; private set; }

        #endregion Members

        #region Methods

        /// <summary>
        /// Request forewarding a lookup request to the node managing the given data hash.
        /// </summary>
        /// <param name="requestingNodeId">The hash identifier of the requesting node.</param>
        /// <param name="dataKeyHash">The hash identifier of the data requested.</param>
        public void RequestKeyLookup(BigInteger requestingNodeId, BigInteger dataKeyHash)
        {

        }

        public void Notify()
        {

        }

        #endregion Methods
    }
}
