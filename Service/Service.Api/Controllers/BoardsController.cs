using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Api.Models;
using Service.Api.Services;

namespace Service.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class BoardsController(IBoardService boardService) : ControllerBase
{
    /// <summary>
    /// Lists boards, optionally filtered by a case-insensitive name/description search.
    /// </summary>
    [HttpGet(Name = "GetBoards")]
    [ProducesResponseType<IReadOnlyList<BoardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> GetBoards([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await boardService.SearchAsync(search, cancellationToken));

    [HttpGet("{id:guid}", Name = "GetBoard")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardDto>> GetBoard(Guid id, CancellationToken cancellationToken)
        => await boardService.GetAsync(id, cancellationToken) is { } board ? Ok(board) : NotFound();

    [HttpPost(Name = "CreateBoard")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BoardDto>> CreateBoard(BoardRequest request, CancellationToken cancellationToken)
    {
        var board = await boardService.CreateAsync(request, cancellationToken);
        return CreatedAtRoute("GetBoard", new { id = board.Id }, board);
    }

    [HttpPut("{id:guid}", Name = "UpdateBoard")]
    [ProducesResponseType<BoardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BoardDto>> UpdateBoard(Guid id, BoardRequest request, CancellationToken cancellationToken)
        => await boardService.UpdateAsync(id, request, cancellationToken) is { } board ? Ok(board) : NotFound();

    [HttpDelete("{id:guid}", Name = "DeleteBoard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBoard(Guid id, CancellationToken cancellationToken)
        => await boardService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Deletes several boards at once. Unknown ids are ignored.
    /// </summary>
    [HttpPost("batch-delete", Name = "DeleteBoards")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteBoards(BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await boardService.DeleteManyAsync(request.Ids, cancellationToken);
        return NoContent();
    }
}
