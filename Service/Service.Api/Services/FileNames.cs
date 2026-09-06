using System.Text;

namespace Service.Api.Services;

/// <summary>Builds safe, readable download file names from user-entered names.</summary>
internal static class FileNames
{
    private const int MaxLength = 50;

    /// <summary>Lower-cases <paramref name="name"/> and replaces whitespace and invalid file name
    /// characters with "-", collapsing repeats and trimming the result. Falls back to
    /// <paramref name="fallback"/> (typically the entity's id) when nothing usable remains.</summary>
    public static string Sanitize(string name, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        var lastWasDash = false;

        foreach (var c in name.Trim().ToLowerInvariant())
        {
            var isDash = char.IsWhiteSpace(c) || Array.IndexOf(invalid, c) >= 0 || c is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|';
            if (isDash)
            {
                if (!lastWasDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                lastWasDash = true;
                continue;
            }

            builder.Append(c);
            lastWasDash = false;

            if (builder.Length >= MaxLength)
            {
                break;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? fallback : slug;
    }
}
