namespace Service.Api.Models;

/// <summary>A lightweight reference to a related entity, used to show linked names without the full graph.</summary>
public sealed record EntityRefDto(Guid Id, string Name);
