using RookRun.Api.Authentication;

namespace RookRun.UnitTest.Api.Authentication;

/// <summary>
/// Unit tests for <see cref="RookRunAuthenticationOptionsValidator"/>.
/// </summary>
public sealed class RookRunAuthenticationOptionsValidatorTests
{
    /// <summary>
    /// Verifies validation succeeds for a complete and valid configuration.
    /// </summary>
    [Fact]
    public void Validate_ReturnsSuccess_ForValidOptions()
    {
        var validator = new RookRunAuthenticationOptionsValidator();
        var options = new RookRunAuthenticationOptions
        {
            Entra = new EntraAuthenticationOptions
            {
                TenantId = "tenant-id",
                ClientId = "client-id",
                ClientSecret = "secret",
                CallbackPath = "/signin-oidc",
                SignedOutCallbackPath = "/signout-callback-oidc"
            },
            AllowedEmailAddresses = ["runner@example.com"]
        };

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Verifies validation fails when required configuration values are missing.
    /// </summary>
    [Fact]
    public void Validate_ReturnsFailure_WhenRequiredValuesAreMissing()
    {
        var validator = new RookRunAuthenticationOptionsValidator();
        var options = new RookRunAuthenticationOptions
        {
            Entra = new EntraAuthenticationOptions(),
            AllowedEmailAddresses = []
        };

        var result = validator.Validate(name: null, options);
        var failures = result.Failures ?? [];

        Assert.False(result.Succeeded);
        Assert.Contains(failures, failure => failure.Contains("Authentication:Entra:TenantId", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("Authentication:AllowedEmailAddresses", StringComparison.Ordinal));
    }
}
