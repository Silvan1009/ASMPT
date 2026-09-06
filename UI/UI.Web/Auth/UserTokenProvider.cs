using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace UI.Web.Auth;

/// <summary>Supplies the current user's Firebase ID token to outgoing Service API calls.</summary>
public interface IUserTokenProvider
{
    /// <summary>The current user's ID token, or null when the request or circuit is anonymous.</summary>
    Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Login request only: after SignInAsync the current request's authentication state is still anonymous,
    /// so the freshly signed-in principal and its tokens are supplied directly.
    /// </summary>
    void UseSession(ClaimsPrincipal principal, FirebaseSession session);
}

public sealed class UserTokenProvider(
    AuthenticationStateProvider authenticationState,
    IFirebaseIdTokenService tokens,
    IIdTokenCache cache) : IUserTokenProvider
{
    private ClaimsPrincipal? _override;

    public void UseSession(ClaimsPrincipal principal, FirebaseSession session)
    {
        _override = principal;
        cache.Set(session.Uid, new CachedIdToken(session.IdToken, session.ExpiresAt));
    }

    public async Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default)
    {
        // Static SSR: the state comes from HttpContext.User. Interactive circuits: from the cookie principal of
        // the SignalR connection. Both carry the refresh-token claim.
        var user = _override ?? (await authenticationState.GetAuthenticationStateAsync()).User;
        return user.Identity?.IsAuthenticated == true
            ? await tokens.GetIdTokenAsync(user, cancellationToken)
            : null;
    }
}
