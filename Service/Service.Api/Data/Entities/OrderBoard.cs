namespace Service.Api.Data.Entities;

/// <summary>
/// Join row for the Order/Board many-to-many relationship. No payload of its own; the foreign keys are
/// discovered by EF Core's naming convention (see <c>Data/Configurations/OrderConfiguration.cs</c>).
/// </summary>
public sealed class OrderBoard
{
    public Guid OrderId { get; init; }

    public Guid BoardId { get; init; }
}
