using Chord.Config;
using Chord.Lib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Linq;
using System.Net;

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
                logger.LogInformation($"Initializing: endpoint={ localEndpoint.Address }:{ localEndpoint.Port }, node id={ BitConverter.ToString(HashingHelper.GetSha1Hash(localEndpoint)) }");

                // initialize a new chord node
                var node = new ChordNode(localEndpoint, logger);
                var bootstrapNode = node.FindBootstrapNode();
                node.JoinNetwork().Wait();

                // attach to process exit event for a graceful shutdown
                AppDomain.CurrentDomain.ProcessExit += (object sender, EventArgs e) => {

                    logger.LogInformation($"Exiting: Graceful exit procedure started");

                    // shutdown chord node gracefully
                    node.LeaveNetwork().Wait();
                    Environment.ExitCode = 0;

                    logger.LogInformation($"Exiting: Graceful exit successful!");
                };

                // write initialization success message to console
                logger.LogInformation($"Initializing: successful! Going into idle state ...");

                // wait until daemon process gets killed from outside
                Console.Read();
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
