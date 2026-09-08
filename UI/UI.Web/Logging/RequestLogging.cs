using System.Security.Claims;
using Serilog.AspNetCore;
using Serilog.Events;

namespace UI.Web.Logging;

/// <summary>
/// Levels and enriches Serilog's one-line-per-request completion event (UseSerilogRequestLogging). Registered
/// outermost in the pipeline (Program.cs).
/// </summary>
internal static class RequestLogging
{
    private static readonly string[] AssetSuffixes = [".css", ".js", ".map", ".woff2", ".woff", ".png", ".svg", ".ico"];

    public static void Configure(RequestLoggingOptions options)
    {
        options.GetLevel = static (httpContext, _, exception) =>
            exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400 ? LogEventLevel.Warning
            : IsInfrastructure(httpContext.Request.Path) ? LogEventLevel.Verbose
            : LogEventLevel.Information;

        options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
        {
            var uid = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uid is not null)
            {
                diagnosticContext.Set("UserId", uid);
            }
        };
    }

    /// <summary>The Blazor Server connection and static assets: high-volume, low-signal requests that would
    /// otherwise dominate the Information log (the /_blazor long-poll/WebSocket request in particular).</summary>
    private static bool IsInfrastructure(PathString path)
        => path.StartsWithSegments("/_blazor") || path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content")
           || (path.Value is { } value && AssetSuffixes.Any(suffix => value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
}
