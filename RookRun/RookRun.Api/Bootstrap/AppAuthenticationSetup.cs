
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RookRun.Api.Authentication;

namespace RookRun.Api.Bootstrap;

static class AppAuthenticationSetup
{
    public static IServiceCollection AddAuthenticationAndAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddAuthentication(configuration)
            .AddAuthorization(configuration);
    }

    public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RookRunAuthenticationOptions>()
            .Bind(configuration.GetSection(RookRunAuthenticationOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<RookRunAuthenticationOptions>, RookRunAuthenticationOptionsValidator>();

        // Add authentication with cookie scheme for session management and OpenID Connect for Microsoft Entra ID integration.
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.AccessDeniedPath = "/auth/access-denied";
                options.LogoutPath = "/auth/sign-out";
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = AuthenticationResponseHandler.HandleApiRedirectAsync(StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = AuthenticationResponseHandler.HandleApiRedirectAsync(StatusCodes.Status403Forbidden)
                };
            })
            .AddOpenIdConnect(options =>
            {
                var authOptions = configuration
                    .GetSection(RookRunAuthenticationOptions.SectionName)
                    .Get<RookRunAuthenticationOptions>()
                    ?? throw new InvalidOperationException("Missing Authentication configuration section.");

                options.Authority = $"https://login.microsoftonline.com/{authOptions.Entra.TenantId}/v2.0";
                options.ClientId = authOptions.Entra.ClientId;
                options.ClientSecret = authOptions.Entra.ClientSecret;
                options.CallbackPath = authOptions.Entra.CallbackPath;
                options.SignedOutCallbackPath = authOptions.Entra.SignedOutCallbackPath;
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name"
                };
                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProviderForSignOut = AuthenticationResponseHandler.HandleOnRedirectToIdentityProviderForSignOutAsync
                };
            });

        return services;
    }

    public static IServiceCollection AddAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAuthorizationHandler, AllowedEmailAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            var allowlistedUserPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AllowedEmailRequirement())
                .Build();

            options.AddPolicy(RookRunAuthorizationPolicyNames.AllowlistedUser, allowlistedUserPolicy);
            options.FallbackPolicy = allowlistedUserPolicy;
        });
        return services;
    }

}