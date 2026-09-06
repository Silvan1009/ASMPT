using System.ComponentModel.DataAnnotations;

namespace Service.Api.Models;

/// <summary>
/// Payload for deleting several entities of the same type at once. Unknown ids are ignored. A plain
/// mutable class, not a record; see the remarks in OrderRequest.cs.
/// </summary>
public sealed class BatchDeleteRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<Guid> Ids { get; set; } = [];
}
