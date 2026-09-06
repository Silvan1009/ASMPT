namespace Service.Api.Data.Entities;

/// <summary>
/// Application user record, created and refreshed from the Firebase ID token on login.
/// Credentials live in Firebase; this table only mirrors the identity data the application needs.
/// </summary>
public sealed class User
{
    /// <summary>Internal identity, independent of the identity provider.</summary>
    public Guid Id { get; init; }

    /// <summary>Firebase user id ("sub" claim). Unique.</summary>
    public required string FirebaseUid { get; init; }

    public required string Email { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>
    /// Last role seen in the user's token (custom claim "role"). Authorization decisions use the token,
    /// not this column.
    /// </summary>
    public string? Role { get; set; }

    public bool EmailVerified { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastLoginAt { get; set; }
}
