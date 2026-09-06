namespace Service.Api.Data.Entities;

/// <summary>
/// Join row for the Board/Component many-to-many relationship. No payload of its own; the foreign keys are
/// discovered by EF Core's naming convention (see <c>Data/Configurations/BoardConfiguration.cs</c>).
/// </summary>
public sealed class BoardComponent
{
    public Guid BoardId { get; init; }

    public Guid ComponentId { get; init; }
}
