namespace Service.Api.Auth;

/// <summary>Claim names as they appear in Firebase ID tokens (inbound claim mapping is disabled).</summary>
public static class FirebaseClaims
{
    public const string Sub = "sub";
    public const string UserId = "user_id";
    public const string Email = "email";
    public const string EmailVerified = "email_verified";
    public const string Name = "name";

    /// <summary>Custom claim set through the Firebase Admin SDK or the emulator UI; carries the application role.</summary>
    public const string Role = "role";
}

public static class FirebaseRoles
{
    public const string Admin = "admin";
}

public static class AuthorizationPolicies
{
    public const string Admin = "Admin";
}
