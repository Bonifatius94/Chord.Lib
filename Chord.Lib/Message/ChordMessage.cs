using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Text;

namespace Chord.Lib.Message
{
    /// <summary>
    /// A proprietary chord message format for key lookup requests using json format.
    /// </summary>
    [JsonObject]
    public class ChordMessage //: IChordMessage
    {
        #region Constructor

        /// <summary>
        /// Empty constructor for Newtonsoft.Json serializer.
        /// </summary>
        public ChordMessage() { }

        /// <summary>
        /// Create a new chord key lookup request message with the given sender and lookup key.
        /// </summary>
        /// <param name="sender">The sender requesting the key lookup.</param>
        /// <param name="lookupKey">The key to be looked up.</param>
        public ChordMessage(ChordEndpoint sender, BigInteger lookupKey)
        {
            Version = "1.0";
            Type = ChordMessageType.KeyLookupRequest;
            RequestId = _random.Next().ToString();
            Requester = sender.Endpoint.ToString();
            LookupKey = HexStringSerializer.Deserialize(lookupKey.ToByteArray());
        }

        /// <summary>
        /// Create a new chord key lookup response message with the given request message and the answer.
        /// </summary>
        /// <param name="request">The original key lookup request message.</param>
        /// <param name="managingNode">The answer to the request.</param>
        public ChordMessage(ChordMessage request, ChordEndpoint managingNode)
        {
            Version = "1.0";
            Type = ChordMessageType.KeyLookupResponse;
            RequestId = request.RequestId;
            Requester = request.Requester;
            LookupKey = request.LookupKey;
            ManagingNode = managingNode.Endpoint.ToString();
        }

        /// <summary>
        /// Create a new chord join request message with the given local endpoint (to be joined).
        /// </summary>
        /// <param name="sender">The sender requesting the join.</param>
        public ChordMessage(ChordEndpoint sender, JoinType joinType)
        {
            Version = "1.0";
            Type = ChordMessageType.JoinRequest;
            RequestId = _random.Next().ToString();
            Requester = sender.Endpoint.ToString();
            JoinType = joinType;
        }

        /// <summary>
        /// Create a new chord join response message from the given join request message and the given finger table.
        /// </summary>
        /// <param name="sender">The sender requesting the join.</param>
        /// <param name="predecessor">The new predecessor of the joining node.</param>
        /// <param name="fingerTable">The finger table of the joining node.</param>
        public ChordMessage(ChordMessage request, ChordEndpoint predecessor, IList<ChordEndpoint> fingerTable)
        {
            Version = "1.0";
            Type = ChordMessageType.KeyLookupResponse;
            RequestId = request.RequestId;
            Requester = request.Requester;
            PredecessorNode = predecessor.Endpoint.ToString();
            FingerTable = fingerTable;
        }

        /// <summary>
        /// Create a new chord live-check message with the given sender and piggy-back join finalization.
        /// </summary>
        /// <param name="sender">The sender requesting the successor / predecessor update.</param>
        /// <param name="finalizeJoin">Indicates whether the join was successful.</param>
        /// <param name="finalizeJoin">Indicates whether the network needs to stabilize.</param>
        public ChordMessage(ChordEndpoint sender, bool finalizeJoin, bool stabilize = false)
        {
            Version = "1.0";
            Type = ChordMessageType.LiveCheck;
            RequestId = _random.Next().ToString();
            Requester = sender.Endpoint.ToString();
            FinalizeJoin = finalizeJoin;
            Stabilize = stabilize;

            // TODO: think of more useful piggy-back attributes
        }

        // TODO: implement stabilization message requesting a token ring update

        #endregion Constructor

        #region Members

        [JsonIgnore]
        private static readonly Random _random = new Random();

        /// <summary>
        /// The chord message protocol version.
        /// </summary>
        [JsonProperty]
        public string Version { get; set; }

        /// <summary>
        /// The chord message type.
        /// </summary>
        [JsonProperty]
        public ChordMessageType Type { get; set; }

        /// <summary>
        /// The chord message's request identification.
        /// </summary>
        [JsonProperty]
        public string RequestId { get; set; }
        // TODO: use timestamp instead of a nonce

        /// <summary>
        /// The chord message's request issuer.
        /// </summary>
        [JsonProperty]
        public string Requester { get; set; }

        /// <summary>
        /// The requesting node as IP endpoint.
        /// </summary>
        [JsonIgnore]
        public IPEndPoint RequesterEndpoint => IPEndPoint.Parse(Requester);

        /// <summary>
        /// The requesting node as chord endpoint.
        /// </summary>
        [JsonIgnore]
        public ChordEndpoint RequesterRemote => new ChordEndpoint(RequesterEndpoint);

        #region JoinSuccessor

        /// <summary>
        /// The predecessor node to be sent a join request.
        /// </summary>
        [JsonProperty]
        public string PredecessorNode { get; set; }

        /// <summary>
        /// The successor's finger table (to save time when joining).
        /// </summary>
        [JsonProperty]
        public IList<ChordEndpoint> FingerTable { get; set; } = new List<ChordEndpoint>();

        /// <summary>
        /// The predecessor node to be sent a join request as IP endpoint.
        /// </summary>
        [JsonIgnore]
        public IPEndPoint PredecessorEndpoint => IPEndPoint.Parse(PredecessorNode);

        /// <summary>
        /// The predecessor node to be sent a join request as chord endpoint.
        /// </summary>
        [JsonIgnore]
        public ChordEndpoint PredecessorRemote => new ChordEndpoint(PredecessorEndpoint);

        /// <summary>
        /// Indicates whether the addressed node should be the new predecessor or successor.
        /// </summary>
        [JsonProperty]
        public JoinType JoinType { get; set; }

        #endregion JoinSuccessor

        #region KeyLookup

        /// <summary>
        /// The key to be looked up.
        /// </summary>
        [JsonProperty]
        public string LookupKey { get; set; }

        /// <summary>
        /// The key to be looked up as numeric hash.
        /// </summary>
        [JsonIgnore]
        public BigInteger LookupKeyNumeric => new BigInteger(HashingHelper.GetSha1Hash(LookupKey));

        /// <summary>
        /// The node managing the key that was looked up (only on response).
        /// </summary>
        [JsonProperty]
        public string ManagingNode { get; set; }

        /// <summary>
        /// The node managing the key that was looked up as IP endpoint.
        /// </summary>
        [JsonIgnore]
        public IPEndPoint ManagingNodeEndpoint => IPEndPoint.Parse(ManagingNode);

        /// <summary>
        /// The node managing the key that was looked up as chord endpoint.
        /// </summary>
        [JsonIgnore]
        public ChordEndpoint ManagingRemote => new ChordEndpoint(ManagingNodeEndpoint);

        #endregion KeyLookup

        #region LiveCheck

        /// <summary>
        /// Piggy-back information whether the pending join was successful.
        /// </summary>
        [JsonProperty]
        public bool FinalizeJoin { get; set; } = false;

        /// <summary>
        /// Piggy-back information whether the node should perform the stabilization mechanism.
        /// </summary>
        [JsonProperty]
        public bool Stabilize { get; set; } = false;

        #endregion LiveCheck

        #endregion Members
    }

    public enum ChordMessageType
    {
        JoinRequest,
        JoinResponse,
        KeyLookupRequest,
        KeyLookupResponse,
        LiveCheck
    }

    public enum JoinType
    {
        JoinSuccessor,
        JoinPredecessor
    }
}
