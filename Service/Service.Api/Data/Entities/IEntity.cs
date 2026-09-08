namespace Service.Api.Data.Entities;

/// <summary>An entity keyed by a <see cref="Guid"/>, so repositories and services can work with ids generically.</summary>
public interface IEntity
{
    Guid Id { get; }
}

/// <summary>An entity with the searchable name/description pair shared by orders, boards and components.</summary>
public interface INamedEntity : IEntity
{
    string Name { get; }

    string? Description { get; }
}
