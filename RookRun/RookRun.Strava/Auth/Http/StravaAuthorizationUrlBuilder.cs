using System.Security.Cryptography;
using System.Text;

namespace RookRun.Strava.Auth.Http;

/// <summary>
/// Builds Strava authorization URIs and generates secure state values.
/// </summary>
public sealed class StravaAuthorizationUrlBuilder
{
    /// <summary>
    /// Creates a cryptographically strong URL-safe state value.
    /// </summary>
    /// <returns>A state value suitable for OAuth callback validation.</returns>
    public string CreateState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Builds the Strava authorization URI for the current flow.
    /// </summary>
    /// <param name="options">The configured client options.</param>
    /// <param name="redirectUri">The effective callback URI.</param>
    /// <param name="state">The flow state value to embed in the request.</param>
    /// <returns>The full authorization URI.</returns>
    public Uri BuildAuthorizeUri(StravaOAuthClientOptions options, Uri redirectUri, string state)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var scope = string.Join(',', options.DefaultScopes.Where(static value => !string.IsNullOrWhiteSpace(value)));
        var query = new StringBuilder();
        AppendQueryParameter(query, "client_id", options.ClientId);
        AppendQueryParameter(query, "redirect_uri", redirectUri.ToString());
        AppendQueryParameter(query, "response_type", "code");
        AppendQueryParameter(query, "approval_prompt", options.ApprovalPrompt);
        AppendQueryParameter(query, "scope", scope);
        AppendQueryParameter(query, "state", state);

        var builder = new UriBuilder(AppendPath(options.AuthorizationBaseUrl, "authorize"))
        {
            Query = query.ToString()
        };

        return builder.Uri;
    }

    /// <summary>
    /// Combines a base URL with a relative path using a single slash separator.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <param name="relativePath">The path to append.</param>
    /// <returns>The combined URL string.</returns>
    private static string AppendPath(string baseUrl, string relativePath) => $"{baseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";

    /// <summary>
    /// Appends a URL-encoded query parameter to the supplied query builder.
    /// </summary>
    /// <param name="builder">The query builder being populated.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    private static void AppendQueryParameter(StringBuilder builder, string name, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append('&');
        }

        builder.Append(Uri.EscapeDataString(name));
        builder.Append('=');
        builder.Append(Uri.EscapeDataString(value));
    }
}
