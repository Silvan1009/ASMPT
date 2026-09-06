using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace UI.Web.Auth;

/// <summary>
/// Validates the cookie session against Firebase on every request. The ID-token cache makes this free in the
/// common case; a definitive refresh failure (user disabled or deleted, token revoked) ends the session,
/// while transient failures keep it, so an outage never logs everyone out.
/// </summary>
public sealed class FirebaseCookieEvents(IFirebaseIdTokenService tokens, ILogger<FirebaseCookieEvents> logger)
    : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (context.Principal is not { Identity.IsAuthenticated: true } principal)
        {
            return;
        }

        try
        {
            await tokens.GetIdTokenAsync(principal, context.HttpContext.RequestAborted);
        }
        catch (SessionInvalidException ex)
        {
            logger.LogInformation(ex, "Firebase session is no longer valid; signing out user {Uid}",
                principal.FindFirstValue(ClaimTypes.NameIdentifier));
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Firebase unreachable while validating the session; keeping the session");
        }
    }
}
