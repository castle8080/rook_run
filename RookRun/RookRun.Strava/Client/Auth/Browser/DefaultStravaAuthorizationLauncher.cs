using RookRun.Strava.Client.Auth.Exceptions;
using System.Diagnostics;

namespace RookRun.Strava.Client.Auth.Browser;

/// <summary>
/// Opens Strava authorization pages with the operating system's default browser.
/// </summary>
public sealed class DefaultStravaAuthorizationLauncher : IStravaAuthorizationLauncher
{
    /// <inheritdoc />
    public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = authorizationUri.ToString(),
                UseShellExecute = true
            });

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new StravaOAuthBrowserLaunchException("Failed to open the system browser for Strava authorization.", ex);
        }
    }
}
