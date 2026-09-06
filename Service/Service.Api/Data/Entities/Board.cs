namespace Service.Api.Data.Entities;

/// <summary>
/// A board design that can be placed in one or more orders and that carries one or more components.
/// Both relationships are many-to-many skip navigations (see <see cref="Order.Boards"/> and
/// <see cref="Component.Boards"/>).
/// </summary>
public sealed class Board
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Length in millimeters.</summary>
    public decimal Length { get; set; }

    /// <summary>Width in millimeters.</summary>
    public decimal Width { get; set; }

    public List<Component> Components { get; } = [];

    public List<Order> Orders { get; } = [];
}
