using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Api.Models;
using Service.Api.Services;

namespace Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class ComponentsController(IComponentService componentService) : ControllerBase
{
    /// <summary>
    /// Lists components, optionally filtered by a case-insensitive name/description search.
    /// </summary>
    [HttpGet(Name = "GetComponents")]
    [ProducesResponseType<IReadOnlyList<ComponentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComponentDto>>> GetComponents([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await componentService.SearchAsync(search, cancellationToken));

    [HttpGet("{id:guid}", Name = "GetComponent")]
    [ProducesResponseType<ComponentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComponentDto>> GetComponent(Guid id, CancellationToken cancellationToken)
        => await componentService.GetAsync(id, cancellationToken) is { } component ? Ok(component) : NotFound();

    [HttpPost(Name = "CreateComponent")]
    [ProducesResponseType<ComponentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ComponentDto>> CreateComponent(ComponentRequest request, CancellationToken cancellationToken)
    {
        var component = await componentService.CreateAsync(request, cancellationToken);
        return CreatedAtRoute("GetComponent", new { id = component.Id }, component);
    }

    [HttpPut("{id:guid}", Name = "UpdateComponent")]
    [ProducesResponseType<ComponentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComponentDto>> UpdateComponent(Guid id, ComponentRequest request, CancellationToken cancellationToken)
        => await componentService.UpdateAsync(id, request, cancellationToken) is { } component ? Ok(component) : NotFound();

    [HttpDelete("{id:guid}", Name = "DeleteComponent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComponent(Guid id, CancellationToken cancellationToken)
        => await componentService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Deletes several components at once. Unknown ids are ignored.
    /// </summary>
    [HttpPost("batch-delete", Name = "DeleteComponents")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteComponents(BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await componentService.DeleteManyAsync(request.Ids, cancellationToken);
        return NoContent();
    }
}
