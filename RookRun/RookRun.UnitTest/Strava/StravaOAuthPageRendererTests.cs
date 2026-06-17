using RookRun.Strava.Client.Auth.Hosting;
using RookRun.Strava.Client.Auth.Models;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="StravaOAuthPageRenderer"/>.
/// </summary>
public sealed class StravaOAuthPageRendererTests
{
    /// <summary>
    /// Verifies the success page includes scope and expiry details.
    /// </summary>
    [Fact]
    public void RenderSuccess_IncludesGrantedScopesAndExpiry()
    {
        var result = new StravaOAuthTokenResult
        {
            AccessToken = "a",
            RefreshToken = "r",
            ExpiresAtUnixTimeSeconds = 1_700_000_000,
            TokenType = "Bearer",
            GrantedScopes = ["activity:read_all", "profile:read_all"]
        };

        var html = StravaOAuthPageRenderer.RenderSuccess(result);

        Assert.Contains("Strava authorization completed", html, StringComparison.Ordinal);
        Assert.Contains("activity:read_all", html, StringComparison.Ordinal);
        Assert.Contains("profile:read_all", html, StringComparison.Ordinal);
        Assert.Contains("UTC", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies rendered success content HTML-encodes scope values.
    /// </summary>
    [Fact]
    public void RenderSuccess_HtmlEncodesScopeValues()
    {
        var result = new StravaOAuthTokenResult
        {
            AccessToken = "a",
            RefreshToken = "r",
            ExpiresAtUnixTimeSeconds = 0,
            TokenType = "Bearer",
            GrantedScopes = ["scope<script>alert(1)</script>"]
        };

        var html = StravaOAuthPageRenderer.RenderSuccess(result);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("scope&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies failure pages include caller-provided title, message, and next-step text.
    /// </summary>
    [Fact]
    public void RenderFailure_UsesProvidedContent()
    {
        var html = StravaOAuthPageRenderer.RenderFailure("Denied", "User denied authorization", "Try again.");

        Assert.Contains("Denied", html, StringComparison.Ordinal);
        Assert.Contains("User denied authorization", html, StringComparison.Ordinal);
        Assert.Contains("Try again.", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies null result input is rejected for success rendering.
    /// </summary>
    [Fact]
    public void RenderSuccess_ThrowsWhenResultIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => StravaOAuthPageRenderer.RenderSuccess(null!));
    }
}
