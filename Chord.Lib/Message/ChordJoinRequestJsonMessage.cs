using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Chord.Lib.Message
{
    [JsonObject]
    public class ChordJoinRequestJsonMessage : IChordMessage
    {
        #region Members

        public ChordMessageType Type { get; set; }

        #endregion Members

        #region Methods

        public byte[] GetAsBinary()
        {
            throw new NotImplementedException();
        }

        #endregion Methods
    }
}
