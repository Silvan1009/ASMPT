using System.Security.Claims;
using Serilog.Context;
using Service.Api.Auth;

namespace Service.Api.Logging;

/// <summary>
/// Pushes UserId into Serilog's LogContext for the rest of the request, so every event written meanwhile —
/// service calls, EF Core, the exception handler — names the caller without each of them reading claims
/// themselves. Registered after UseAuthentication (Program.cs), so the principal is populated.
/// </summary>
internal sealed class UserLogContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        var uid = user.FindFirstValue(FirebaseClaims.Sub) ?? user.FindFirstValue(FirebaseClaims.UserId);
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
