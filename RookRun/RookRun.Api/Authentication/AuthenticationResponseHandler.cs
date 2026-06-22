using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace RookRun.Api.Authentication;

using OpenIdRedirectContext = Microsoft.AspNetCore.Authentication.OpenIdConnect.RedirectContext;
using CookieRedirectContext = Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions>;

public sealed class AuthenticationResponseHandler
{
    public static Task HandleOnRedirectToIdentityProviderForSignOutAsync(OpenIdRedirectContext context)
    {
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
        context.ProtocolMessage.PostLogoutRedirectUri = $"{baseUrl}/auth/signed-out";
        return Task.CompletedTask;
    }

    public static Func<CookieRedirectContext, Task> HandleApiRedirectAsync(int statusCode)
    {
        return context =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = statusCode;
            }
            return Task.CompletedTask;
        };
    }
}
