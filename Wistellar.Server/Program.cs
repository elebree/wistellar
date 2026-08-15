using Wistellar.Server.Config;
using Wistellar.Server.Services;

namespace Wistellar.Server
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine(Properties.Resources.AsciiBanner);

            // Handle command line arguments for user management
            if (args.Length > 0)
            {
                await CommandLineService.HandleCommandLineArguments(args);
                return; // Exit after handling command line operations
            }

            var builder = WebApplication.CreateBuilder(args);

            builder.ConfigureServices();

            var app = builder.Build();

            // Configure the application pipeline
            app.ConfigureApplication();

            await app.RunAsync();
        }
    }
}