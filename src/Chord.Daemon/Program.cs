using Chord.Config;
using Chord.Lib;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;

namespace Chord.Daemon
{
    public class Program
    {
        // app shutdown snippet source: https://stackoverflow.com/questions/50646549/docker-graceful-shutdown-from-dotnet-core-2-0-app
        // logger factory usage: https://docs.microsoft.com/de-de/aspnet/core/migration/logging-nonaspnetcore?view=aspnetcore-3.0#21-to-30

        // TODO: transform into async task
        public static void Main(string[] args)
        {
            // TODO: think about useful program args

            try
            {
                // initialize the logger and a new chord node
                var logger = initLogger();
                var node = initChord(logger);

                // attach to process exit event for a graceful shutdown
                AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) => {

                    // shutdown chord node gracefully
                    logger.LogInformation($"Exiting: Graceful exit procedure started");
                    node.LeaveNetwork().Wait();
                    Environment.ExitCode = 0;
                    logger.LogInformation($"Exiting: Graceful exit successful!");
                };

                // test the chord network performance by issuing lookup requests
                testLookupPerformance(node, logger);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Unexpected error occurred!");
                Console.WriteLine(ex);
                Environment.ExitCode = -1;
            }
        }

        private static ILogger initLogger()
        {
            using (var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole()))
            {
                return loggerFactory.CreateLogger("chord");
            }
        }

        private static ChordNode initChord(ILogger logger)
        {
            // retrieve the node's IP address and port from the local IP configuration
            var local = new IPEndPoint(IpSettings.GetChordIpv4Address(), IpSettings.GetChordPort());

            logger.LogInformation($"Initializing: endpoint={ local.Address }:{ local.Port }, " +
                $"node id={ HexString.Deserialize(HashingHelper.GetSha1Hash(local)) }");

            var netId = IpSettings.GetIpv4NetworkId();
            var broadcast = IpSettings.GetIpv4Broadcast();

            // connect the chord node to the chord network
            var node = new ChordNode(local, logger);
            node.FindBootstrapNode(netId, broadcast)
                .ContinueWith(x => node.JoinNetwork(x.Result)).Wait();

            // write initialization success message to console
            logger.LogInformation($"Initializing: successful! Going into idle state ...");

            return node;
        }

        private static void testLookupPerformance(ChordNode node, ILogger logger)
        {
            // initialize random number generator
            using (var rng = new RNGCryptoServiceProvider())
            {
                // send random key lookup messages for testing the chord network
                while (true)
                {
                    // generate a random key
                    byte[] bytes = new byte[20];
                    rng.GetBytes(bytes);

                    // send a lookup request for the generated key
                    node.LookupKey(new BigInteger(bytes))
                        .ContinueWith(e => 
                            logger.LogInformation(
                                $"Lookup: key '{ HexString.Deserialize(bytes) }' " +
                                $"is managed by node with id '{ HexString.Deserialize(e.Result.NodeId.ToByteArray()) }'"))
                        .Wait();

                    // sleep for 1 sec
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
