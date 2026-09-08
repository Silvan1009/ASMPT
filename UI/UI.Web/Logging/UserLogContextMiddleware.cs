using System.Security.Claims;
using Serilog.Context;

namespace UI.Web.Logging;

/// <summary>
/// Pushes UserId into Serilog's LogContext for the rest of the request. Registered after UseAuthentication
/// (Program.cs). The /_blazor connection request passes through here too, so the property flows into every
/// hub invocation for that circuit's lifetime, alongside CircuitId (see Logging/TraceCircuitHandler.cs).
/// </summary>
internal sealed class UserLogContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var uid = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return uid is null ? next(context) : InvokeWithUserAsync(context, uid);
    }

    private async Task InvokeWithUserAsync(HttpContext context, string uid)
    {
        using (LogContext.PushProperty("UserId", uid))
        {
            await next(context);
        }
    }
}
