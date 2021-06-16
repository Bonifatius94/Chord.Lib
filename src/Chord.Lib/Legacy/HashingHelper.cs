using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Chord.Lib
{
    /// <summary>
    /// Helper class for generating 160-bit SHA-1 hashes required for the chord protocol.
    /// </summary>
    public static class HashingHelper
    {
        #region Methods

        /// <summary>
        /// Compute the SHA-1 hash of the given IP address and port.
        /// </summary>
        /// <param name="ip">The IP address to be hashed.</param>
        /// <param name="port">The port to be hashed.</param>
        /// <returns>a 160-bit SHA-1 hash of the given data</returns>
        public static byte[] GetSha1Hash(IPEndPoint endpoint)
        {
            var textToBeHashed = $"{ endpoint.Address }:{ endpoint.Port }";
            return GetSha1Hash(textToBeHashed);
        }

        /// <summary>
        /// Compute the SHA-1 hash of the given text data.
        /// </summary>
        /// <param name="textData">The text data to be hashed.</param>
        /// <returns>a 160-bit SHA-1 hash of the given data</returns>
        public static byte[] GetSha1Hash(string textData)
        {
            var bytesToHash = Convert.FromBase64String(textData);
            return GetSha1Hash(bytesToHash);
        }

        /// <summary>
        /// Compute the SHA-1 hash of the given binary data.
        /// </summary>
        /// <param name="binaryData">The binary data to be hashed.</param>
        /// <returns>a 160-bit SHA-1 hash of the given data</returns>
        public static byte[] GetSha1Hash(byte[] binaryData)
        {
            // initialize a SHA-1 hash generator
            using (var sha1 = new SHA1Managed())
            {
                // compute the hash from byte array
                return sha1.ComputeHash(binaryData);
            }
        }

        /// <summary>
        /// Compute the SHA-1 hash of the given binary stream data.
        /// </summary>
        /// <param name="binaryData">The binary stream data to be hashed.</param>
        /// <returns>a 160-bit SHA-1 hash of the given data</returns>
        public static byte[] GetSha1Hash(Stream stream)
        {
            // initialize a SHA-1 hash generator
            using (var sha1 = new SHA1Managed())
            {
                // TODO: figure out whether the stream needs to be prepared before reading

                // compute the hash from byte stream
                return sha1.ComputeHash(stream);
            }
        }

        ///// <summary>
        ///// Convert the given hash into a hex string.
        ///// </summary>
        ///// <param name="hash">The hash to be converted.</param>
        ///// <returns>the hex string representing the hash</returns>
        //public static string HashToString(byte[] hash)
        //{
        //    var builder = new StringBuilder(hash.Length * 2);

        //    foreach (byte b in hash)
        //    {
        //        builder.Append(b.ToString("X2"));
        //    }

        //    return builder.ToString();
        //}

        #endregion Methods
    }
}
