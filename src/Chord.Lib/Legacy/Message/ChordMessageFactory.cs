// using Newtonsoft.Json;
// using System;
// using System.Collections.Generic;
// using System.Text;

// namespace Chord.Lib.Message
// {
//     /// <summary>
//     /// A factory class for creating chord messages.
//     /// </summary>
//     public static class ChordMessageFactory
//     {
//         #region Methods

//         /// <summary>
//         /// Serialize the given chord message as binary data.
//         /// </summary>
//         /// <param name="message">The chord message to be serialized.</param>
//         /// <returns>json content as byte array</returns>
//         public static byte[] GetAsBinary(ChordMessage message)
//         {
//             string json = JsonConvert.SerializeObject(message);
//             return Encoding.UTF8.GetBytes(json);
//         }

//         /// <summary>
//         /// Deserialize a chord message from the given binary data.
//         /// </summary>
//         /// <param name="data">The binary data chord message content to be deserialized.</param>
//         /// <returns>a chord message instance</returns>
//         public static ChordMessage FromBinary(byte[] data)
//         {
//             string json = Encoding.UTF8.GetString(data);
//             return JsonConvert.DeserializeObject<ChordMessage>(json);
//         }

//         #endregion Methods
//     }
// }
