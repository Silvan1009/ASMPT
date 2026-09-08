namespace UI.Web.ApiClient;

/// <summary>
/// What the production grids need from a row: its id and display name. Implemented by the generated DTOs
/// through the partial declarations below (NSwag emits every DTO as a partial class), so the shared grid base
/// class works without touching generated code.
/// </summary>
public interface IProductionItem
{
    Guid Id { get; }

    string Name { get; }
}

public partial class OrderDto : IProductionItem;

public partial class BoardDto : IProductionItem;

public partial class ComponentDto : IProductionItem;
