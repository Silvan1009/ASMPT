namespace UI.Web.Auth;

public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
}

/// <summary>Values of the Firebase custom claim "role".</summary>
public static class FirebaseRoles
{
    public const string Admin = "admin";
}

/// <summary>Application-private claim types stored in the authentication cookie.</summary>
public static class FirebaseClaimTypes
{
    /// <summary>
    /// The Firebase refresh token. It lives in the cookie because interactive Blazor circuits only see the
    /// ClaimsPrincipal; the cookie itself is encrypted by Data Protection. Never render this claim.
    /// </summary>
    public const string RefreshToken = "firebase:refresh_token";
}
