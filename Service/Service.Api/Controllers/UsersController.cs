using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Api.Auth;
using Service.Api.Models;
using Service.Api.Services;

namespace Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>
    /// Lists all users known to the application. Requires the admin role.
    /// </summary>
    [HttpGet(Name = "GetUsers")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        return Ok(await userService.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// Creates or refreshes the caller's user row from the validated token. The UI calls this after login.
    /// </summary>
    [HttpPut("me", Name = "SyncCurrentUser")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> SyncCurrentUser(CancellationToken cancellationToken)
    {
        return Ok(await userService.SyncCurrentUserAsync(User, cancellationToken));
    }
}
