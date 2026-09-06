namespace Service.Api.Models;

/// <summary>A component as exposed by the API, with its boards as lightweight references.</summary>
public sealed record ComponentDto(
    Guid Id,
    string Name,
    string? Description,
    int Quantity,
    IReadOnlyList<EntityRefDto> Boards);
