using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RookRun.Api.Authentication;
using RookRun.Contracts;

namespace RookRun.Api.Controllers;

/// <summary>
/// Exposes authentication endpoints for interactive login, logout, and access denied handling.
/// </summary>
[Route("auth")]
public sealed class AuthController : Controller
{
    private readonly IAuthorizationService authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used for policy evaluation.</param>
    public AuthController(IAuthorizationService authorizationService)
    {
        this.authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
    }

    /// <summary>
    /// Returns the current authentication state and user identity.
    /// </summary>
    /// <returns>An object describing whether the user is authenticated, their email, and authorization status.</returns>
    [AllowAnonymous]
    [HttpGet("me")]
    public async Task<UserAuthDto> GetCurrentUser()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return new UserAuthDto { IsAuthenticated = false };
        }

        var email = User.FindFirst("email")?.Value ?? User.Identity?.Name;
        var authorizationResult = await this.authorizationService.AuthorizeAsync(
            User,
            policyName: RookRunAuthorizationPolicyNames.AllowlistedUser);

        return new UserAuthDto
        {
            IsAuthenticated = true,
            Email = email,
            IsAuthorized = authorizationResult.Succeeded
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
    public IActionResult AccessDenied()
    {
        return View();
    }

    /// <summary>
    /// Returns a sign-out confirmation page.
    /// </summary>
    /// <returns>An HTML response confirming sign-out and offering a link to sign back in.</returns>
    [AllowAnonymous]
    [HttpGet("signed-out")]
    public IActionResult SignedOut()
    {
        return View();
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
