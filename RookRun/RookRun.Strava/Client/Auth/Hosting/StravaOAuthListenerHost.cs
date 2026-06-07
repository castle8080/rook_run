using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using RookRun.Strava.Client.Auth.Coordination;
using RookRun.Strava.Client.Auth.Exceptions;

namespace RookRun.Strava.Client.Auth.Hosting;

/// <summary>
/// Hosts the temporary loopback web server used for Strava OAuth callbacks.
/// </summary>
public sealed class StravaOAuthListenerHost : IStravaOAuthListenerHost
{
    /// <inheritdoc />
    public async Task<IStravaOAuthListenerSession> StartAsync(
        StravaOAuthListenerOptions options,
        StravaOAuthCallbackCoordinator coordinator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(coordinator);
        ValidateHost(options.CallbackHost);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(BuildListenerUrl(options));

        var app = builder.Build();
        MapEndpoints(app, options, coordinator);
        await app.StartAsync(cancellationToken);

        var baseAddress = GetEffectiveBaseAddress(app);
        var callbackUri = new Uri(baseAddress, options.CallbackPath.TrimStart('/'));
        var successUri = new Uri(baseAddress, options.SuccessPath.TrimStart('/'));
        return new StravaOAuthListenerSession(app, callbackUri, successUri);
    }

    /// <summary>
    /// Maps the callback and terminal browser endpoints onto the temporary host.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <param name="options">The active listener options.</param>
    /// <param name="coordinator">The flow coordinator used to resolve callback results.</param>
    private static void MapEndpoints(WebApplication app, StravaOAuthListenerOptions options, StravaOAuthCallbackCoordinator coordinator)
    {
        app.MapGet(options.CallbackPath, context => HandleCallbackAsync(context, coordinator));
        app.MapGet(options.SuccessPath, static async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(StravaOAuthPageRenderer.RenderFailure(
                "Strava authorization status",
                "This authorization session has already completed.",
                "You may close this window."));
        });
    }

    /// <summary>
    /// Handles the Strava OAuth callback request, including state validation.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="coordinator">The flow coordinator used to resolve callback results.</param>
    private static async Task HandleCallbackAsync(HttpContext context, StravaOAuthCallbackCoordinator coordinator)
    {
        var state = context.Request.Query["state"].ToString();
        var code = context.Request.Query["code"].ToString();
        var error = context.Request.Query["error"].ToString();

        if (!coordinator.TryGetPendingFlow(state, out _))
        {
            await WriteFailureAsync(context, StatusCodes.Status400BadRequest, "Strava authorization failed", "This authorization session is invalid or expired. You may close this window.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            var exception = string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase)
                ? new StravaOAuthAccessDeniedException("The Strava authorization request was denied by the user.")
                : new StravaOAuthException("The Strava authorization request failed.");

            coordinator.CompleteFailure(state, exception);
            await WriteFailureAsync(context, StatusCodes.Status400BadRequest, "Strava authorization failed", "Authorization was denied. You may close this window.");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            coordinator.CompleteFailure(state, new StravaOAuthException("The Strava authorization callback did not include an authorization code."));
            await WriteFailureAsync(context, StatusCodes.Status400BadRequest, "Strava authorization failed", "Authorization could not be completed. You may close this window.");
            return;
        }

        try
        {
            if (!coordinator.CompleteSuccess(state, code))
            {
                await WriteFailureAsync(context, StatusCodes.Status410Gone, "Strava authorization failed", "This authorization session is no longer active. You may close this window.");
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(StravaOAuthPageRenderer.RenderFailure(
                "Strava authorization received",
                "Authorization completed. You may close this window.",
                "You may close this window."));
        }
        catch (Exception ex)
        {
            coordinator.CompleteFailure(state, new StravaOAuthException("Authorization callback handling failed.", ex));
            await WriteFailureAsync(context, StatusCodes.Status500InternalServerError, "Strava authorization failed", "Authorization callback handling failed. You may close this window.");
        }
    }

    /// <summary>
    /// Writes a browser-visible failure page for a terminal callback error.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="title">The page title to render.</param>
    /// <param name="message">The user-facing failure message.</param>
    private static async Task WriteFailureAsync(HttpContext context, int statusCode, string title, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(StravaOAuthPageRenderer.RenderFailure(title, message, "You may close this window."));
    }

    /// <summary>
    /// Builds the listener URL used to bind the temporary loopback host.
    /// </summary>
    /// <param name="options">The listener configuration.</param>
    /// <returns>The listener URL.</returns>
    private static string BuildListenerUrl(StravaOAuthListenerOptions options)
    {
        var port = options.CallbackPort ?? 0;
        return $"http://{options.CallbackHost}:{port}";
    }

    /// <summary>
    /// Resolves the effective base address reported by the running web application.
    /// </summary>
    /// <param name="app">The running web application.</param>
    /// <returns>The effective base address.</returns>
    private static Uri GetEffectiveBaseAddress(WebApplication app)
    {
        var address = app.Urls.SingleOrDefault();
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("The Strava OAuth listener did not report a bound address.");
        }

        return new Uri(address.EndsWith('/') ? address : $"{address}/", UriKind.Absolute);
    }

    /// <summary>
    /// Ensures the configured callback host is limited to loopback addresses.
    /// </summary>
    /// <param name="callbackHost">The host to validate.</param>
    private static void ValidateHost(string callbackHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackHost);

        if (!string.Equals(callbackHost, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(callbackHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            throw new StravaOAuthException("Strava OAuth loopback hosting only supports localhost or 127.0.0.1.");
        }
    }
}
