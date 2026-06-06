using System.Net;
using RookRun.Strava.Auth.Models;
using System.Text;

namespace RookRun.Strava.Auth.Hosting;

/// <summary>
/// Renders simple inline HTML pages for browser-visible OAuth outcomes.
/// </summary>
public static class StravaOAuthPageRenderer
{
    /// <summary>
    /// Renders the success page displayed after a completed authorization flow.
    /// </summary>
    /// <param name="result">The token result used to show non-sensitive completion details.</param>
    /// <returns>The rendered HTML page.</returns>
    public static string RenderSuccess(StravaOAuthTokenResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var detailsBuilder = new StringBuilder();

        if (result.GrantedScopes.Count > 0)
        {
            detailsBuilder.AppendLine("<div class=\"section\"><div class=\"label\">Granted scopes</div><ul>");
            foreach (var scope in result.GrantedScopes)
            {
                detailsBuilder.Append("<li>").Append(WebUtility.HtmlEncode(scope)).AppendLine("</li>");
            }

            detailsBuilder.AppendLine("</ul></div>");
        }

        if (result.ExpiresAtUnixTimeSeconds > 0)
        {
            detailsBuilder.Append("<div class=\"section\"><div class=\"label\">Access token expires</div><div>")
                .Append(WebUtility.HtmlEncode(DateTimeOffset.FromUnixTimeSeconds(result.ExpiresAtUnixTimeSeconds).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")))
                .AppendLine("</div></div>");
        }

        return RenderPage(
            title: "Strava authorization completed",
            message: "Your Strava connection was established successfully.",
            accentColor: "#166534",
            details: detailsBuilder.ToString(),
            nextStep: "You may now close this window and return to the app.");
    }

    /// <summary>
    /// Renders a failure page displayed for browser-visible authorization errors.
    /// </summary>
    /// <param name="title">The page title.</param>
    /// <param name="message">The main error message.</param>
    /// <param name="nextStep">Optional follow-up guidance for the user.</param>
    /// <returns>The rendered HTML page.</returns>
    public static string RenderFailure(string title, string message, string? nextStep = null)
    {
        return RenderPage(title, message, "#991b1b", string.Empty, nextStep ?? "Return to the application and try again.");
    }

    /// <summary>
    /// Builds the common HTML shell used by success and failure pages.
    /// </summary>
    /// <param name="title">The page title.</param>
    /// <param name="message">The main message.</param>
    /// <param name="accentColor">The accent color applied to the card.</param>
    /// <param name="details">Additional HTML details to insert into the page body.</param>
    /// <param name="nextStep">The follow-up guidance message.</param>
    /// <returns>The rendered HTML page.</returns>
    private static string RenderPage(string title, string message, string accentColor, string details, string nextStep)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("    <meta charset=\"utf-8\" />");
        builder.Append("    <title>").Append(WebUtility.HtmlEncode(title)).AppendLine("</title>");
        builder.AppendLine("    <style>");
        builder.AppendLine("        :root { color-scheme: light; }");
        builder.AppendLine("        body { margin: 0; font-family: system-ui, -apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif; background: #f5f7fb; color: #111827; }");
        builder.AppendLine("        .shell { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; }");
        builder.Append("        .card { width: min(480px, 100%); background: #ffffff; border-radius: 16px; border-top: 8px solid ").Append(accentColor).AppendLine("; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.12); padding: 28px; }");
        builder.AppendLine("        h1 { margin: 0 0 12px; font-size: 1.5rem; }");
        builder.AppendLine("        p { margin: 0; line-height: 1.5; }");
        builder.AppendLine("        .section { margin-top: 18px; }");
        builder.AppendLine("        .label { font-weight: 600; margin-bottom: 8px; }");
        builder.AppendLine("        ul { margin: 0; padding-left: 20px; }");
        builder.AppendLine("        .next-step { margin-top: 18px; color: #374151; }");
        builder.AppendLine("    </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("    <main class=\"shell\">");
        builder.AppendLine("        <section class=\"card\">");
        builder.Append("            <h1>").Append(WebUtility.HtmlEncode(title)).AppendLine("</h1>");
        builder.Append("            <p>").Append(WebUtility.HtmlEncode(message)).AppendLine("</p>");
        builder.AppendLine(details);
        builder.Append("            <p class=\"next-step\">").Append(WebUtility.HtmlEncode(nextStep)).AppendLine("</p>");
        builder.AppendLine("        </section>");
        builder.AppendLine("    </main>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }
}
