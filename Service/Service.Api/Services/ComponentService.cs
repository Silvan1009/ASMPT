using Service.Api.Data.Entities;
using Service.Api.Models;
using Service.Api.Repositories;

namespace Service.Api.Services;

public sealed class ComponentService(IComponentRepository components) : IComponentService
{
    public async Task<IReadOnlyList<ComponentDto>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => (await components.SearchAsync(search, cancellationToken)).Select(ToDto).ToList();

    public async Task<ComponentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await components.GetWithBoardsAsync(id, cancellationToken) is { } component ? ToDto(component) : null;

    public async Task<ComponentDto> CreateAsync(ComponentRequest request, CancellationToken cancellationToken = default)
    {
        var component = new Component
        {
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            Quantity = request.Quantity,
        };

        await components.AddAsync(component, cancellationToken);
        await components.SaveChangesAsync(cancellationToken);
        return ToDto(component);
    }

    public async Task<ComponentDto?> UpdateAsync(Guid id, ComponentRequest request, CancellationToken cancellationToken = default)
    {
        var component = await components.GetWithBoardsAsync(id, cancellationToken);
        if (component is null)
        {
            return null;
        }

        component.Name = request.Name.Trim();
        component.Description = Normalize(request.Description);
        component.Quantity = request.Quantity;

        await components.SaveChangesAsync(cancellationToken);
        return ToDto(component);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => await components.DeleteByIdsAsync([id], cancellationToken) == 1;

    public Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => components.DeleteByIdsAsync(ids, cancellationToken);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ComponentDto ToDto(Component component) => new(
        component.Id,
        component.Name,
        component.Description,
        component.Quantity,
        component.Boards.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).Select(b => new EntityRefDto(b.Id, b.Name)).ToList());
}
