namespace Service.Api.Data.Entities;

/// <summary>
/// A production order for one or more boards. Boards can be shared between orders (see
/// <see cref="Board.Orders"/>), so the relationship is modeled as a many-to-many skip navigation.
/// </summary>
public sealed class Order : INamedEntity
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateOnly OrderDate { get; set; }

    public List<Board> Boards { get; } = [];
}
