using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Chord.Lib.Message
{
    /// <summary>
    /// A proprietary chord message format for key lookup responses using json format.
    /// </summary>
    [JsonObject]
    public class ChordKeyLookupJsonResponseMessage : IChordMessage
    {
        #region Constructor

        /// <summary>
        /// Empty constructor for Newtonsoft.Json deserializer.
        /// </summary>
        public ChordKeyLookupJsonResponseMessage() { }

        /// <summary>
        /// Create a new chord key lookup response message from the related request message.
        /// </summary>
        /// <param name="request">The related request message.</param>
        public ChordKeyLookupJsonResponseMessage(ChordKeyLookupJsonRequestMessage request) : this(request.RequestId, request.LookupKey) { }

        /// <summary>
        /// Create a new chord key lookup response message with the given request id and lookup key.
        /// </summary>
        /// <param name="requestId">The id of the related request.</param>
        /// <param name="lookupKey">The key to be looked up.</param>
        public ChordKeyLookupJsonResponseMessage(string requestId, string lookupKey)
        {
            Version = "1.0";
            Type = ChordMessageType.KeyLookupRequest;
            RequestId = requestId;
            LookupKey = lookupKey;
        }

        #endregion Constructor

        #region Members

        [JsonProperty]
        public string Version { get; set; }

        [JsonProperty]
        public ChordMessageType Type { get; set; }

        [JsonProperty]
        public string RequestId { get; set; }

        [JsonProperty]
        public string LookupKey { get; set; }

        [JsonIgnore]
        public BigInteger LookupKeyNumeric => new BigInteger(HexStringSerializer.Serialize(LookupKey));

        #endregion Members

        #region Methods

        /// <summary>
        /// Serialize the message as byte array.
        /// </summary>
        /// <returns>json content as byte array</returns>
        public byte[] GetAsBinary()
        {
            string json = JsonConvert.SerializeObject(this);
            return Encoding.UTF8.GetBytes(json);
        }

        #endregion Methods
    }
}
