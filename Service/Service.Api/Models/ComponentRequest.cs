using System.ComponentModel.DataAnnotations;

namespace Service.Api.Models;

/// <summary>
/// Payload for creating or updating a component. Used for both POST and PUT. A plain mutable class, not a
/// record; see the remarks in OrderRequest.cs.
/// </summary>
public sealed class ComponentRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    public int Quantity { get; set; }
}
