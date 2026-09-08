using System.Security.Claims;
using Service.Api.Auth;
using Service.Api.Data.Entities;
using Service.Api.Models;
using Service.Api.Repositories;

namespace Service.Api.Services;

public sealed class UserService(IRepository<User> users, TimeProvider clock, ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto> SyncCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var uid = principal.GetFirebaseUid();
        var email = principal.GetEmail()
            ?? throw new InvalidOperationException("The token carries no email claim.");
        var now = clock.GetUtcNow();   // zero offset: required by Npgsql for timestamptz columns

        var user = await users.FirstOrDefaultAsync(u => u.FirebaseUid == uid, cancellationToken);
        var isNewUser = user is null;
        if (user is null)
        {
            user = new User
            {
                FirebaseUid = uid,
                Email = email,
                CreatedAt = now,
                LastLoginAt = now,
            };
            await users.AddAsync(user, cancellationToken);
        }

        // The entity is tracked, so the assignments below are persisted by SaveChangesAsync.
        user.Email = email;
        user.DisplayName = principal.GetDisplayName();
        user.Role = principal.GetRole();
        user.EmailVerified = principal.GetEmailVerified();
        user.LastLoginAt = now;

        await users.SaveChangesAsync(cancellationToken);
        // Email is never logged: the uid is the stable, non-personal identifier for this user in the logs.
        logger.LogInformation("User {Uid} synchronised ({Outcome})", uid, isNewUser ? "created" : "updated");
        return ToDto(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await users.GetAllAsync(cancellationToken);
        return all
            .OrderBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.FirebaseUid,
        user.Email,
        user.DisplayName,
        user.Role,
        user.EmailVerified,
        user.CreatedAt,
        user.LastLoginAt);
}
