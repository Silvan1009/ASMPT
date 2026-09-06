namespace Service.Api.Models;

/// <summary>A board as exposed by the API, with its components and orders as lightweight references.</summary>
public sealed record BoardDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Length,
    decimal Width,
    IReadOnlyList<EntityRefDto> Components,
    IReadOnlyList<EntityRefDto> Orders);
