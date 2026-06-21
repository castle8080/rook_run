using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Api.Authentication;

namespace RookRun.UnitTest.Api.Authentication;

/// <summary>
/// Unit tests for <see cref="AllowedEmailAuthorizationHandler"/>.
/// </summary>
public sealed class AllowedEmailAuthorizationHandlerTests
{
    /// <summary>
    /// Verifies an authenticated principal succeeds authorization when email is allowlisted.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_Succeeds_ForAllowlistedEmail()
    {
        var requirement = new AllowedEmailRequirement();
        var principal = CreateAuthenticatedPrincipal("runner@example.com");
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        var optionsMonitor = new StubOptionsMonitor<RookRunAuthenticationOptions>(new RookRunAuthenticationOptions
        {
            AllowedEmailAddresses = ["runner@example.com"]
        });
        var logger = new Mock<ILogger<AllowedEmailAuthorizationHandler>>();
        var sut = new AllowedEmailAuthorizationHandler(optionsMonitor, logger.Object);

        await sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>
    /// Verifies authorization fails when the authenticated principal email is not allowlisted.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_ForNonAllowlistedEmail()
    {
        var requirement = new AllowedEmailRequirement();
        var principal = CreateAuthenticatedPrincipal("runner@example.com");
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        var optionsMonitor = new StubOptionsMonitor<RookRunAuthenticationOptions>(new RookRunAuthenticationOptions
        {
            AllowedEmailAddresses = ["other@example.com"]
        });
        var logger = new Mock<ILogger<AllowedEmailAuthorizationHandler>>();
        var sut = new AllowedEmailAuthorizationHandler(optionsMonitor, logger.Object);

        await sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Verifies authorization fails when the authenticated principal has no email claim.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_DoesNotSucceed_WhenEmailClaimIsMissing()
    {
        var requirement = new AllowedEmailRequirement();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "oidc"));
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        var optionsMonitor = new StubOptionsMonitor<RookRunAuthenticationOptions>(new RookRunAuthenticationOptions
        {
            AllowedEmailAddresses = ["runner@example.com"]
        });
        var logger = new Mock<ILogger<AllowedEmailAuthorizationHandler>>();
        var sut = new AllowedEmailAuthorizationHandler(optionsMonitor, logger.Object);

        await sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Creates an authenticated principal with an email claim.
    /// </summary>
    /// <param name="email">The email claim value.</param>
    /// <returns>A claims principal representing an authenticated user.</returns>
    private static ClaimsPrincipal CreateAuthenticatedPrincipal(string email)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("email", email),
            new Claim("name", "Runner")
        ],
        "oidc");

        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Minimal options monitor implementation for deterministic unit tests.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    private sealed class StubOptionsMonitor<T> : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        private readonly T value;

        /// <summary>
        /// Initializes a new instance of the <see cref="StubOptionsMonitor{T}"/> class.
        /// </summary>
        /// <param name="value">The options value to expose.</param>
        public StubOptionsMonitor(T value)
        {
            this.value = value;
        }

        /// <summary>
        /// Gets the current options value.
        /// </summary>
        public T CurrentValue => this.value;

        /// <summary>
        /// Returns the current options value regardless of name.
        /// </summary>
        /// <param name="name">The options name.</param>
        /// <returns>The configured options value.</returns>
        public T Get(string? name) => this.value;

        /// <summary>
        /// Registers a no-op change callback for test usage.
        /// </summary>
        /// <param name="listener">The listener delegate.</param>
        /// <returns>A disposable callback registration.</returns>
        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;

        /// <summary>
        /// Disposable singleton used for no-op registrations.
        /// </summary>
        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            /// <summary>
            /// Releases resources.
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
