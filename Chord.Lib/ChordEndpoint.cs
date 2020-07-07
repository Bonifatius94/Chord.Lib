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
    public class ChordEndpoint
    {
        #region Constructor

        /// <summary>
        /// Initialize a new chord node with the given IP endpoint configuration.
        /// </summary>
        /// <param name="endpoint">The remote node's IP endpoint configuration.</param>
        public ChordEndpoint(IPEndPoint endpoint)
        {
            NodeId = new BigInteger(HashingHelper.GetSha1Hash(endpoint));
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

        // TODO: add endpoint state enum property indicating the connection health

        #endregion Members
    }
}
