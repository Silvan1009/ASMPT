namespace Service.Api.Models;

/// <summary>An order as exposed by the API, with its boards as lightweight references.</summary>
public sealed record OrderDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly OrderDate,
    IReadOnlyList<EntityRefDto> Boards);
