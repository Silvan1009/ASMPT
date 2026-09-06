namespace Service.Api.ErrorHandling;

/// <summary>
/// Thrown by a service when a request references ids of a related entity that do not exist (e.g. an order
/// naming an unknown board id). Mapped to a 400 validation problem by
/// <see cref="RelatedEntitiesNotFoundExceptionHandler"/>.
/// </summary>
public sealed class RelatedEntitiesNotFoundException(string propertyName, string entityName, IReadOnlyCollection<Guid> missingIds)
    : Exception($"Unknown {entityName} id(s): {string.Join(", ", missingIds)}.")
{
    /// <summary>Name of the request property the missing ids came from (e.g. "BoardIds"), so the client can
    /// show the error on the right field.</summary>
    public string PropertyName { get; } = propertyName;

    public IReadOnlyCollection<Guid> MissingIds { get; } = missingIds;
}
