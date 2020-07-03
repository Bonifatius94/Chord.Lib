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
    public class ChordKeyLookupJsonRequestMessage : IChordMessage
    {
        #region Constructor

        /// <summary>
        /// Empty constructor for Newtonsoft.Json deserializer.
        /// </summary>
        public ChordKeyLookupJsonRequestMessage() { }

        /// <summary>
        /// Create a new chord key lookup request message with the given sender and lookup key.
        /// </summary>
        /// <param name="sender">The sender requesting the key lookup.</param>
        /// <param name="lookupKey">The key to be looked up.</param>
        public ChordKeyLookupJsonRequestMessage(IPEndPoint sender, BigInteger lookupKey)
        {
            Version = "1.0";
            Type = ChordMessageType.KeyLookupRequest;
            RequestId = _random.Next().ToString();
            Requester = $"{ sender.Address }:{ sender.Port }";
            LookupKey = HexStringSerializer.Deserialize(lookupKey.ToByteArray());

            // TODO: use timestamp instead of a nonce request id
        }

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

        /// <summary>
        /// The chord message's request issuer.
        /// </summary>
        [JsonProperty]
        public string Requester { get; set; }

        /// <summary>
        /// The key to be looked up.
        /// </summary>
        [JsonProperty]
        public string LookupKey { get; set; }

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
