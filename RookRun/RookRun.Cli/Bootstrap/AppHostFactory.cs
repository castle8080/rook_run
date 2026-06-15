using Microsoft.Extensions.Hosting;

namespace RookRun.Cli.Bootstrap;

internal static class AppHostFactory
{
    public static IHost Create(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        AppConfigurationSetup.Configure(builder, args);
        AppLoggingSetup.Configure(builder);
        AppServicesSetup.Configure(builder.Services, builder.Configuration);

        return builder.Build();
    }
}
