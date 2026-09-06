namespace Service.Api.Models;

/// <summary>
/// The order download payload: the simulated hand-off to a production line. Carries the full graph (boards
/// and their components) rather than references, since the file is meant to stand on its own.
/// </summary>
public sealed record OrderExportDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly OrderDate,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<BoardExportDto> Boards);

public sealed record BoardExportDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Length,
    decimal Width,
    IReadOnlyList<ComponentExportDto> Components);

public sealed record ComponentExportDto(
    Guid Id,
    string Name,
    string? Description,
    int Quantity);
