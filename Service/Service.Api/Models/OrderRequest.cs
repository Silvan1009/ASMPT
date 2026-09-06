using System.ComponentModel.DataAnnotations;

namespace Service.Api.Models;

/// <summary>
/// Payload for creating or updating an order. Used for both POST and PUT.
/// A plain mutable class rather than a record: ASP.NET Core's record-aware model validation reads
/// DataAnnotations from the primary constructor's parameters, while the OpenAPI schema generator reads
/// them from properties; a "[property: ...]" target satisfies the latter but makes the former throw at
/// request time. An ordinary class with settable properties has no such ambiguity.
/// </summary>
public sealed class OrderRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = "";

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateOnly OrderDate { get; set; }

    [Required, MinLength(1)]
    public IReadOnlyList<Guid> BoardIds { get; set; } = [];
}
