using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Api.Models;
using Service.Api.Services;

namespace Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class EchoController(IEchoService echoService) : ControllerBase
{
    /// <summary>
    /// Echoes the supplied message back to the caller.
    /// </summary>
    /// <param name="message">The message to echo.</param>
    [HttpGet(Name = "Echo")]
    [ProducesResponseType<EchoResponse>(StatusCodes.Status200OK)]
    public ActionResult<EchoResponse> Echo([FromQuery] string message)
    {
        return echoService.Echo(message);
    }
}
