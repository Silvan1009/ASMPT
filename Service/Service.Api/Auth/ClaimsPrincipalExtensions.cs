using System.Security.Claims;

namespace Service.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The Firebase user id ("sub", falling back to "user_id").</summary>
    public static string GetFirebaseUid(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FirebaseClaims.Sub)
        ?? principal.FindFirstValue(FirebaseClaims.UserId)
        ?? throw new InvalidOperationException("The token carries no Firebase user id claim.");

    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FirebaseClaims.Email);

    public static string? GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FirebaseClaims.Name);

    public static string? GetRole(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FirebaseClaims.Role);

    public static bool GetEmailVerified(this ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirstValue(FirebaseClaims.EmailVerified), out var verified) && verified;
}
