using Microsoft.AspNetCore.Authorization;

namespace RookRun.Api.Authentication;

/// <summary>
/// Represents a requirement that an authenticated user email is included in the configured allowlist.
/// </summary>
public sealed class AllowedEmailRequirement : IAuthorizationRequirement
{
}
