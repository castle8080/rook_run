using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace RookRun.Api.Authentication;

/// <summary>
/// Evaluates whether the current authenticated user's email is allowed to access RookRun.
/// </summary>
public sealed class AllowedEmailAuthorizationHandler : AuthorizationHandler<AllowedEmailRequirement>
{
    private readonly IOptionsMonitor<RookRunAuthenticationOptions> authenticationOptionsMonitor;
    private readonly ILogger<AllowedEmailAuthorizationHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllowedEmailAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="authenticationOptionsMonitor">The monitor for authentication options.</param>
    /// <param name="logger">The logger for authorization decisions.</param>
    public AllowedEmailAuthorizationHandler(
        IOptionsMonitor<RookRunAuthenticationOptions> authenticationOptionsMonitor,
        ILogger<AllowedEmailAuthorizationHandler> logger)
    {
        this.authenticationOptionsMonitor = authenticationOptionsMonitor ?? throw new ArgumentNullException(nameof(authenticationOptionsMonitor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles allowlist requirement evaluation for the current principal.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The requirement being evaluated.</param>
    /// <returns>A task that completes when evaluation finishes.</returns>
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowedEmailRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var email = ResolveEmail(context.User);
        if (string.IsNullOrWhiteSpace(email))
        {
            this.logger.LogWarning("Authenticated principal did not include an email claim.");
            return Task.CompletedTask;
        }

        var allowedEmails = this.authenticationOptionsMonitor.CurrentValue.AllowedEmailAddresses;
        if (allowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        this.logger.LogWarning("Access denied for authenticated principal with email {Email}.", email);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves the canonical email value from known email-oriented claim types.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <returns>The resolved email value when present; otherwise <see langword="null"/>.</returns>
    private static string? ResolveEmail(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("email")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("upn");
    }
}
