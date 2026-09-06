using Microsoft.AspNetCore.Mvc;
using Service.Api.Models;

namespace Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EchoController : ControllerBase
{
    /// <summary>
    /// Echoes the supplied message back to the caller.
    /// </summary>
    /// <param name="message">The message to echo.</param>
    [HttpGet(Name = "Echo")]
    [ProducesResponseType<EchoResponse>(StatusCodes.Status200OK)]
    public ActionResult<EchoResponse> Echo([FromQuery] string message)
    {
        return new EchoResponse(message);
    }
}
