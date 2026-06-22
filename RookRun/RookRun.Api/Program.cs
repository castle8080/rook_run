using Microsoft.Extensions.Options;
using RookRun.Api.Authentication;
using RookRun.Api.Bootstrap;

namespace RookRun.Api;

internal class Program
{
    static int Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddControllersWithViews();
        builder.Services.AddOpenApi();

        builder.Services.AddAuthenticationAndAuthorization(builder.Configuration);
        builder.Services.AddCompression();
        builder.Services.AddServices(builder.Configuration);

        var app = builder.Build();

        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RookRun.Api.AuthenticationStartup");
        var startupAuthOptions = app.Services.GetRequiredService<IOptions<RookRunAuthenticationOptions>>().Value;
        startupLogger.LogInformation(
            "Authentication configured. AllowedEmailCount={AllowedEmailCount}",
            startupAuthOptions.AllowedEmailAddresses.Length);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseResponseCompression();

        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<RookRun.Api.Middleware.RequestResponseLoggingMiddleware>();

        app.MapControllers();

        // Never serve the SPA shell for unknown API routes.
        app.MapFallback("/api/{*path}", () => Results.NotFound())
            .AllowAnonymous();

        // Serve the Blazor app for non-file routes (including deep links like /activities)
        // and require the same allowlisted policy as API endpoints before loading the SPA shell.
        app.MapFallbackToFile("index.html")
            .RequireAuthorization(RookRunAuthorizationPolicyNames.AllowlistedUser);

        app.Run();

        return 0;
    }
}