using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace RookRun.Cli.Bootstrap;

internal static class AppConfigurationSetup
{
    public static void Configure(HostApplicationBuilder builder, string[] args)
    {
        builder.Configuration.Sources.Clear();

        var configurationBuilder = builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

        configurationBuilder.AddUserSecrets<Program>(optional: true);

        configurationBuilder.AddEnvironmentVariables();

        if (args.Length > 0)
        {
            builder.Configuration.AddCommandLine(args);
        }
    }
}
