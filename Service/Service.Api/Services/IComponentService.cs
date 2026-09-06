using Service.Api.Models;

namespace Service.Api.Services;

public interface IComponentService
{
    Task<IReadOnlyList<ComponentDto>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Null when no such component exists.</summary>
    Task<ComponentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ComponentDto> CreateAsync(ComponentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null when no such component exists.</summary>
    Task<ComponentDto?> UpdateAsync(Guid id, ComponentRequest request, CancellationToken cancellationToken = default);

    /// <summary>False when no such component exists.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes several components at once; unknown ids are ignored.</summary>
    Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
