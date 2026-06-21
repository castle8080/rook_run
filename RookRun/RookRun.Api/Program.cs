using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RookRun.Api.Authentication;
using RookRun.Job.DependencyInjection;
using RookRun.ObjectStore.DependencyInjection;
using RookRun.Strava.DependencyInjection;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<RookRunAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(RookRunAuthenticationOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<RookRunAuthenticationOptions>, RookRunAuthenticationOptionsValidator>();

builder.Services.AddControllers();
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();
builder.Services
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
            OnRedirectToLogin = context => HandleApiRedirectAsync(context, StatusCodes.Status401Unauthorized),
            OnRedirectToAccessDenied = context => HandleApiRedirectAsync(context, StatusCodes.Status403Forbidden)
        };
    })
    .AddOpenIdConnect(options =>
    {
        var authOptions = builder.Configuration
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
            OnRedirectToIdentityProviderForSignOut = context =>
            {
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                context.ProtocolMessage.PostLogoutRedirectUri = $"{baseUrl}/auth/signed-out";
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, AllowedEmailAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    var allowlistedUserPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new AllowedEmailRequirement())
        .Build();

    options.AddPolicy(RookRunAuthorizationPolicyNames.AllowlistedUser, allowlistedUserPolicy);
    options.FallbackPolicy = allowlistedUserPolicy;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/octet-stream",
        "application/wasm"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services
    .AddObjectStore(builder.Configuration)
    .AddStravaActivities(builder.Configuration)
    .AddJobs(builder.Configuration);

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RookRun.Api.AuthenticationStartup");
var startupAuthOptions = app.Services.GetRequiredService<IOptions<RookRunAuthenticationOptions>>().Value;
startupLogger.LogInformation(
    "Authentication configured. AllowedEmailCount={AllowedEmailCount}",
    startupAuthOptions.AllowedEmailAddresses.Length);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseResponseCompression();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<RookRun.Api.Middleware.RequestResponseLoggingMiddleware>();

app.MapControllers();

// Never serve the SPA shell for unknown API routes.
app.MapFallback("/api/{*path}", () => Results.NotFound())
    .AllowAnonymous();

// Serve the Blazor app for non-file routes (including deep links like /activities)
// and require the same allowlisted policy as API endpoints before loading the SPA shell.
app.MapFallbackToFile("index.html")
    .RequireAuthorization(RookRunAuthorizationPolicyNames.AllowlistedUser);

app.Run();

/// <summary>
/// Returns status codes for API authentication redirects so API callers get 401/403 instead of HTML redirects.
/// </summary>
/// <param name="context">The redirect context raised by cookie authentication.</param>
/// <param name="statusCode">The status code to return for API requests.</param>
/// <returns>A task that completes when redirect handling finishes.</returns>
static Task HandleApiRedirectAsync(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

public partial class Program;
