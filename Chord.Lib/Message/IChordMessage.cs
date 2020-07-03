using System;
using System.Collections.Generic;
using System.Text;

namespace Chord.Lib
{
    /// <summary>
    /// An interface for implementations of the chord protocol's message payload (proprietary) sent via UDP.
    /// </summary>
    public interface IChordMessage
    {
        #region Members

        ChordMessageType Type { get; set; }

        #endregion Members

        #region Methods

        /// <summary>
        /// Retrieve the chord message's content as byte array.
        /// </summary>
        /// <returns>the chord message's content as byte array</returns>
        byte[] GetAsBinary();

        #endregion Methods
    }

    public enum ChordMessageType
    {
        KeyLookupRequest,
        KeyLookupResponse,
        Notification
    }
}
