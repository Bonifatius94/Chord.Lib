using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Chord.Lib.Message
{
    /// <summary>
    /// A factory class for creating chord messages.
    /// </summary>
    public static class ChordMessageFactory
    {
        #region Methods

        /// <summary>
        /// Deserialize a new json message from the given UTF-8 binary content.
        /// </summary>
        /// <param name="content">The UTF-8 content to parse.</param>
        /// <returns>a chord message</returns>
        public static IChordMessage DeserializeMessage(byte[] content)
        {
            // convert UTF-8 content to json string
            string json = Encoding.UTF8.GetString(content);

            // only parse the base parameters of the json string for switching
            var baseMessage = JsonConvert.DeserializeObject<ChordJsonBaseMessage>(json);

            // initialize the exact message type
            switch (baseMessage.Type)
            {
                case ChordMessageType.KeyLookupRequest: return JsonConvert.DeserializeObject<ChordKeyLookupJsonRequestMessage>(json);
                case ChordMessageType.KeyLookupResponse: return JsonConvert.DeserializeObject<ChordKeyLookupJsonResponseMessage>(json);
                case ChordMessageType.Notification: return JsonConvert.DeserializeObject<ChordNotificationMessage>(json);
                default: throw new ArgumentException($"Unknown content detected! Cannot parse message content '{ json }'!");
            }
        }

        #endregion Methods
    }

    /// <summary>
    /// A proprietary chord message format for retrieving message base data using json format.
    /// </summary>
    [JsonObject]
    class ChordJsonBaseMessage
    {
        #region Members

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

        #endregion Members
    }
}
