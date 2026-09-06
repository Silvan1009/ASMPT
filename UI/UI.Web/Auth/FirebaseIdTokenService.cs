using System.Security.Claims;

namespace UI.Web.Auth;

/// <summary>Provides a valid Firebase ID token for a cookie principal, refreshing it through the cache when needed.</summary>
public interface IFirebaseIdTokenService
{
    Task<string> GetIdTokenAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class FirebaseIdTokenService(IIdTokenCache cache, IFirebaseAuthClient firebase) : IFirebaseIdTokenService
{
    public async Task<string> GetIdTokenAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new SessionInvalidException("The session carries no user id.");
        var refreshToken = user.FindFirstValue(FirebaseClaimTypes.RefreshToken)
            ?? throw new SessionInvalidException("The session carries no refresh token.");

        var token = await cache.GetOrRefreshAsync(uid, async ct =>
        {
            var session = await firebase.RefreshIdTokenAsync(refreshToken, ct);
            return new CachedIdToken(session.IdToken, session.ExpiresAt);
        }, cancellationToken);

        return token.IdToken;
    }
}
