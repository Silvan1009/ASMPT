namespace Service.Api.Data.Entities;

/// <summary>
/// A component that can be placed on one or more boards (see <see cref="Board.Components"/>).
/// </summary>
public sealed class Component : INamedEntity
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int Quantity { get; set; }

    public List<Board> Boards { get; } = [];
}
