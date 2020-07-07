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

        public static void Main(string[] args)
        {
            // TODO: think about useful program args

            try
            {
                // initialize console logger
                ILogger logger;
                using (var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole())) { logger = loggerFactory.CreateLogger("chord"); }

                // retrieve the node's IP address and port from the local IP configuration
                var localEndpoint = new IPEndPoint(IpSettingsHelper.GetChordIpv4Address(), IpSettingsHelper.GetChordPort());

                // write initialization message to console
                logger.LogInformation($"Initializing: endpoint={ localEndpoint.Address }:{ localEndpoint.Port }, " +
                    $"node id={ HexStringSerializer.Deserialize(HashingHelper.GetSha1Hash(localEndpoint)) }");

                // initialize a new chord node
                var node = new ChordNode(localEndpoint, logger);
                node.FindBootstrapNode(IpSettingsHelper.GetIpv4NetworkId(), IpSettingsHelper.GetIpv4Broadcast())
                    .ContinueWith(bootstrapNode => node.JoinNetwork(bootstrapNode.Result))
                    .Wait();

                // attach to process exit event for a graceful shutdown
                AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) =>
                {
                    logger.LogInformation($"Exiting: Graceful exit procedure started");

                    // shutdown chord node gracefully
                    node.LeaveNetwork().Wait();
                    Environment.ExitCode = 0;

                    logger.LogInformation($"Exiting: Graceful exit successful!");
                };

                // write initialization success message to console
                logger.LogInformation($"Initializing: successful! Going into idle state ...");

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
                            .ContinueWith(key => 
                                logger.LogInformation(
                                    $"Lookup: key '{ HexStringSerializer.Deserialize(bytes) }' " +
                                    $"is managed by node with id '{ HexStringSerializer.Deserialize(key.Result.ToByteArray()) }'"))
                            .Wait();

                        // sleep for 1 sec
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Unexpected error occurred!");
                Console.WriteLine(ex);
                Environment.ExitCode = -1;
            }
        }
    }
}
