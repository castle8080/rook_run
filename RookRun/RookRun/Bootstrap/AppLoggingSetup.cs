using Microsoft.Extensions.Hosting;
using Serilog;

namespace RookRun.Bootstrap;

internal static class AppLoggingSetup
{
    public static void Configure(HostApplicationBuilder builder)
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "var", "logs", "app-.log");

        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day));
    }
}
