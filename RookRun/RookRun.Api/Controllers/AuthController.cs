using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RookRun.Contracts;

namespace RookRun.Api.Controllers;

/// <summary>
/// Exposes authentication endpoints for interactive login, logout, and access denied handling.
/// </summary>
[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Returns the current authentication state and user identity.
    /// </summary>
    /// <returns>An object describing whether the user is authenticated and their email if so.</returns>
    [AllowAnonymous]
    [HttpGet("me")]
    public UserAuthDto GetCurrentUser()
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return new UserAuthDto { IsAuthenticated = false };
        }

        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        return new UserAuthDto
        {
            IsAuthenticated = true,
            Email = email
        };
    }

    /// <summary>
    /// Starts an OpenID Connect sign-in challenge.
    /// </summary>
    /// <param name="returnUrl">An optional local return URL after successful authentication.</param>
    /// <returns>A challenge result that redirects to Microsoft Entra ID.</returns>
    [AllowAnonymous]
    [HttpGet("sign-in")]
    public IActionResult SignIn([FromQuery] string? returnUrl = "/")
    {
        var redirectUri = BuildRedirectUri(returnUrl);
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Signs the user out of local cookie and Microsoft Entra sessions.
    /// </summary>
    /// <param name="returnUrl">An optional local return URL after sign-out completes.</param>
    /// <returns>A sign-out result for cookie and OpenID Connect schemes.</returns>
    [AllowAnonymous]
    [HttpGet("sign-out")]
    public IActionResult SignOut([FromQuery] string? returnUrl = "/")
    {
        var redirectUri = BuildRedirectUri(returnUrl);
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return SignOut(
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Returns a simple access denied page for authenticated users not in the email allowlist.
    /// </summary>
    /// <returns>An HTML response describing denied access and recovery actions.</returns>
    [AllowAnonymous]
    [HttpGet("access-denied")]
    public ContentResult AccessDenied()
    {
        const string content = """
<!doctype html>
<html lang=\"en\">
<head>
  <meta charset=\"utf-8\" />
  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
  <title>RookRun - Access Denied</title>
  <style>
    body { font-family: Segoe UI, sans-serif; background: #f8fafc; color: #1f2937; margin: 0; }
    main { max-width: 44rem; margin: 6rem auto; padding: 2rem; background: white; border: 1px solid #e5e7eb; border-radius: 0.75rem; box-shadow: 0 10px 40px rgba(15, 23, 42, 0.08); }
    h1 { margin-top: 0; }
    .actions { display: flex; gap: 0.75rem; margin-top: 1.25rem; }
    a { text-decoration: none; border: 1px solid #cbd5e1; border-radius: 0.5rem; padding: 0.6rem 1rem; color: #0f172a; }
    a.primary { background: #0f172a; color: white; border-color: #0f172a; }
  </style>
</head>
<body>
  <main>
    <h1>Access denied</h1>
    <p>Your account is authenticated, but it is not approved to use this application.</p>
    <p>Ask an administrator to add your email address to the RookRun allowlist.</p>
    <div class=\"actions\">
      <a href=\"/auth/sign-out\">Sign out</a>
      <a class=\"primary\" href=\"/auth/sign-in\">Retry sign in</a>
    </div>
  </main>
</body>
</html>
""";

        return Content(content, "text/html");
    }

    /// <summary>
    /// Returns a sign-out confirmation page.
    /// </summary>
    /// <returns>An HTML response confirming sign-out and offering a link to sign back in.</returns>
    [AllowAnonymous]
    [HttpGet("signed-out")]
    public ContentResult SignedOut()
    {
        const string content = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>RookRun - Signed Out</title>
  <style>
    body { font-family: Segoe UI, sans-serif; background: #f8fafc; color: #1f2937; margin: 0; }
    main { max-width: 44rem; margin: 6rem auto; padding: 2rem; background: white; border: 1px solid #e5e7eb; border-radius: 0.75rem; box-shadow: 0 10px 40px rgba(15, 23, 42, 0.08); }
    h1 { margin-top: 0; }
    .actions { display: flex; gap: 0.75rem; margin-top: 1.25rem; }
    a { text-decoration: none; border: 1px solid #cbd5e1; border-radius: 0.5rem; padding: 0.6rem 1rem; color: #0f172a; }
    a.primary { background: #0f172a; color: white; border-color: #0f172a; }
  </style>
  <script>
    window.addEventListener('load', function() {
      setTimeout(function() {
        localStorage.clear();
        sessionStorage.clear();
        location.reload();
      }, 2000);
    });
  </script>
</head>
<body>
  <main>
    <h1>Signed out</h1>
    <p>You have been successfully signed out of RookRun. Refreshing in 2 seconds...</p>
    <div class="actions">
      <a class="primary" href="#" onclick="location.reload(); return false;">Refresh now</a>
    </div>
  </main>
</body>
</html>
""";

        return Content(content, "text/html");
    }

    /// <summary>
    /// Builds a safe local redirect target for authentication endpoints.
    /// </summary>
    /// <param name="returnUrl">The user-provided return URL.</param>
    /// <returns>A safe local URL, defaulting to root when input is invalid.</returns>
    private string BuildRedirectUri(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        return Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
    }
}
