using Chord.Lib.Message;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chord.Lib.Protocol
{
    /// <summary>
    ///  An implementation of the chord server.
    /// </summary>
    public class ChordServer
    {
        #region Methods

        /// <summary>
        /// Handle incoming chord messages from other peers for the given endpoint asynchronously and invoke handler actions. 
        /// The task can be stopped gracefully using the given cancellation token.
        /// </summary>
        /// <param name="local">The local endpoint to be listened to.</param>
        /// <param name="handler">The message handler delegate.</param>
        /// <param name="token">The cancellation to exit gracefully.</param>
        /// <returns>a task handle</returns>
        public async Task ListenMessages(IPEndPoint local, Action<IChordMessage> handler, CancellationToken token)
        {
            var tasks = new List<Task>();

            try
            {
                // check if the task is already cancelled
                token.ThrowIfCancellationRequested();

                // create udp client sniffing on local chord IP endpoint
                using (var client = new UdpClient(local))
                {
                    // loop endless (until the task gets cancelled)
                    while (true)
                    {
                        // listen to message async
                        var result = await client.ReceiveAsync();

                        // parse the message
                        var message = ChordMessageFactory.DeserializeMessage(result.Buffer);

                        // invoke the event handler
                        var task = new Task(() => handler.Invoke(message));
                        task.GetAwaiter().OnCompleted(() => tasks.Remove(task));
                        tasks.Add(task);
                        task.Start();

                        // check if a cancellation is requested
                        if (token.IsCancellationRequested)
                        {
                            // wait until all started jobs are finished
                            Task.WaitAll(tasks.ToArray());

                            // quit the task gracefully
                            token.ThrowIfCancellationRequested();
                        }
                    }
                }
            }
            catch (Exception /*ex*/) { }
        }

        #endregion Methods
    }
}
