using System.Security.Claims;
using Serilog.AspNetCore;
using Serilog.Events;
using Service.Api.Auth;

namespace Service.Api.Logging;

/// <summary>
/// Levels and enriches Serilog's one-line-per-request completion event (UseSerilogRequestLogging). Registered
/// outermost in the pipeline (Program.cs), before UseExceptionHandler/UseStatusCodePages, so GetLevel always
/// sees the final status code: a RelatedEntitiesNotFoundException that the exception handler maps to a 400 is
/// logged here as a 400, not as the 500 it would look like from inside the handler.
/// </summary>
internal static class RequestLogging
{
    public static void Configure(RequestLoggingOptions options)
    {
        options.GetLevel = static (httpContext, _, exception) =>
            exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400 ? LogEventLevel.Warning
            : LogEventLevel.Information;

        // IncludeQueryInRequestPath stays false (the default): "?search=" carries user-entered text.
        options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
        {
            var user = httpContext.User;
            var uid = user.FindFirstValue(FirebaseClaims.Sub) ?? user.FindFirstValue(FirebaseClaims.UserId);
            if (uid is not null)
            {
                diagnosticContext.Set("UserId", uid);
            }
        };
    }
}
