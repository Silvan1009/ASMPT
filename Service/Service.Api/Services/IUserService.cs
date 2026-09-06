using System.Security.Claims;
using Service.Api.Models;

namespace Service.Api.Services;

/// <summary>
/// Maintains the application's user table from Firebase identities. The controller depends on this
/// interface so it can be unit-tested with a fake implementation.
/// </summary>
public interface IUserService
{
    /// <summary>Creates or refreshes the user row for the authenticated principal and returns it.</summary>
    Task<UserDto> SyncCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
