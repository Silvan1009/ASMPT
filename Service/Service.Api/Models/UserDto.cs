namespace Service.Api.Models;

/// <summary>A row of the application's user table as exposed by the API.</summary>
public sealed record UserDto(
    Guid Id,
    string FirebaseUid,
    string Email,
    string? DisplayName,
    string? Role,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt);
