using System.Text;

namespace Service.Api.Repositories;

/// <summary>Builds escaped patterns for <c>EF.Functions.ILike</c> search queries.</summary>
internal static class LikePatterns
{
    /// <summary>Escape character passed as the third argument to <c>EF.Functions.ILike</c>.</summary>
    public const string Escape = "\\";

    /// <summary>Builds a "contains" pattern (<c>%term%</c>) with <c>\</c>, <c>%</c> and <c>_</c> escaped so
    /// user input cannot be read as ILIKE wildcards.</summary>
    public static string Contains(string term)
    {
        var builder = new StringBuilder(term.Length + 2).Append('%');
        foreach (var c in term)
        {
            if (c is '\\' or '%' or '_')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.Append('%').ToString();
    }
}
