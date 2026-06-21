using Microsoft.Extensions.Options;

namespace RookRun.Api.Authentication;

/// <summary>
/// Validates <see cref="RookRunAuthenticationOptions"/> values at startup.
/// </summary>
public sealed class RookRunAuthenticationOptionsValidator : IValidateOptions<RookRunAuthenticationOptions>
{
    /// <summary>
    /// Validates the configured authentication options.
    /// </summary>
    /// <param name="name">The options name.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>A validation result describing success or configuration errors.</returns>
    public ValidateOptionsResult Validate(string? name, RookRunAuthenticationOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Authentication options are required.");
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Entra.TenantId))
        {
            failures.Add("Authentication:Entra:TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Entra.ClientId))
        {
            failures.Add("Authentication:Entra:ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Entra.ClientSecret))
        {
            failures.Add("Authentication:Entra:ClientSecret is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Entra.CallbackPath))
        {
            failures.Add("Authentication:Entra:CallbackPath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Entra.SignedOutCallbackPath))
        {
            failures.Add("Authentication:Entra:SignedOutCallbackPath is required.");
        }

        if (options.AllowedEmailAddresses.Length == 0)
        {
            failures.Add("Authentication:AllowedEmailAddresses must include at least one email address.");
        }

        if (options.AllowedEmailAddresses.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Authentication:AllowedEmailAddresses cannot include empty values.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
