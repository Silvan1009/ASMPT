using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Api.Models;
using Service.Api.Services;

namespace Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>
    /// Lists orders, optionally filtered by a case-insensitive name/description search.
    /// </summary>
    [HttpGet(Name = "GetOrders")]
    [ProducesResponseType<IReadOnlyList<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetOrders([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await orderService.SearchAsync(search, cancellationToken));

    [HttpGet("{id:guid}", Name = "GetOrder")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(Guid id, CancellationToken cancellationToken)
        => await orderService.GetAsync(id, cancellationToken) is { } order ? Ok(order) : NotFound();

    [HttpPost(Name = "CreateOrder")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> CreateOrder(OrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orderService.CreateAsync(request, cancellationToken);
        return CreatedAtRoute("GetOrder", new { id = order.Id }, order);
    }

    [HttpPut("{id:guid}", Name = "UpdateOrder")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> UpdateOrder(Guid id, OrderRequest request, CancellationToken cancellationToken)
        => await orderService.UpdateAsync(id, request, cancellationToken) is { } order ? Ok(order) : NotFound();

    [HttpDelete("{id:guid}", Name = "DeleteOrder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrder(Guid id, CancellationToken cancellationToken)
        => await orderService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Deletes several orders at once. Unknown ids are ignored.
    /// </summary>
    [HttpPost("batch-delete", Name = "DeleteOrders")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteOrders(BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await orderService.DeleteManyAsync(request.Ids, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Downloads the order (with its boards and their components) as a JSON file: the simulated hand-off to
    /// a production line.
    /// </summary>
    [HttpGet("{id:guid}/download", Name = "DownloadOrder")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadOrder(Guid id, CancellationToken cancellationToken)
    {
        var file = await orderService.ExportAsync(id, cancellationToken);
        return file is null ? NotFound() : File(file.Content, "application/octet-stream", file.FileName);
    }
}
