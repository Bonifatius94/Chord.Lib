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

        /// <summary>
        /// The remote node's assumed health state.
        /// </summary>
        public EndpointState State { get; set; } = EndpointState.Live;

        #endregion Members
    }

    /// <summary>
    /// An enumeration modeling health states of remote nodes.
    /// </summary>
    public enum EndpointState
    {
        /// <summary>
        /// The remote endpoint is assumed to be up and well functioning.
        /// </summary>
        Live,

        /// <summary>
        /// It is unclear whether the remote node is still up. The live-check timeout is awaited to be sure.
        /// </summary>
        Questioning,

        /// <summary>
        /// The remote endpoint is assumed to be dead. Connection is lost.
        /// </summary>
        Dead,
    }
}
