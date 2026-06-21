namespace RookRun.Api.Authentication;

/// <summary>
/// Represents top-level authentication and authorization configuration for the API host.
/// </summary>
public sealed class RookRunAuthenticationOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind these options.
    /// </summary>
    public const string SectionName = "Authentication";

    /// <summary>
    /// Gets or sets Microsoft Entra OpenID Connect settings.
    /// </summary>
    public EntraAuthenticationOptions Entra { get; set; } = new();

    /// <summary>
    /// Gets or sets the set of exact email addresses allowed to use the application.
    /// </summary>
    public string[] AllowedEmailAddresses { get; set; } = [];
}

/// <summary>
/// Represents Microsoft Entra-specific OpenID Connect options.
/// </summary>
public sealed class EntraAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the Microsoft Entra tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Microsoft Entra app registration client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret used for confidential client authentication.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the callback path handled by OpenID Connect middleware.
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>
    /// Gets or sets the callback path invoked by Entra after sign-out.
    /// </summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
}
