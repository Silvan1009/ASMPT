using Microsoft.EntityFrameworkCore;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

/// <summary>The one definition of the name/description search behind every entity list endpoint.</summary>
internal static class SearchQueryExtensions
{
    /// <summary>Keeps the entities whose name or description contains <paramref name="search"/>
    /// (case-insensitive; ILIKE wildcards in the term are escaped). No-op when the term is null or blank.</summary>
    public static IQueryable<TEntity> WhereNameOrDescriptionContains<TEntity>(this IQueryable<TEntity> query, string? search)
        where TEntity : class, INamedEntity
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = LikePatterns.Contains(search.Trim());
        return query.Where(e =>
            EF.Functions.ILike(e.Name, pattern, LikePatterns.Escape) ||
            (e.Description != null && EF.Functions.ILike(e.Description, pattern, LikePatterns.Escape)));
    }
}
