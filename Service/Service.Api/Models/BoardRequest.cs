using System.ComponentModel.DataAnnotations;

namespace Service.Api.Models;

/// <summary>
/// Payload for creating or updating a board. Used for both POST and PUT. Length and width are in
/// millimeters. A plain mutable class, not a record; see the remarks in OrderRequest.cs.
/// </summary>
public sealed class BoardRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(0.01, 100000)]
    public decimal Length { get; set; }

    [Range(0.01, 100000)]
    public decimal Width { get; set; }

    [Required, MinLength(1)]
    public IReadOnlyList<Guid> ComponentIds { get; set; } = [];
}
