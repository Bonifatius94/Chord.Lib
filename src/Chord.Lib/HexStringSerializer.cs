using System;
using System.Collections.Generic;
using System.Text;

namespace Chord.Lib
{
    /// <summary>
    /// A helper class for conversions between hex strings and byte arrays.
    /// </summary>
    public static class HexString
    {
        #region Methods

        // conversion snippet source: https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa

        /// <summary>
        /// Convert the given hex string to a byte array.
        /// </summary>
        /// <param name="hex">The hex string to be converted.</param>
        /// <returns>a byte array representing the hex string data.</returns>
        public static byte[] Serialize(string hex)
        {
            // make sure that the hex bits length is even
            if (hex.Length % 2 != 0) { throw new ArgumentException("Invalid hex length detected! Must be an even length!"); }

            // init byte array
            byte[] bytes = new byte[hex.Length / 2];

            // loop through hex pairs and write the data to the byte array
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }

            return bytes;
        }

        /// <summary>
        /// Convert the given byte array to a hex string.
        /// </summary>
        /// <param name="bytes">The byte array to be converted.</param>
        /// <returns>a hex string representing the byte array.</returns>
        public static string Deserialize(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        #endregion Methods
    }
}
