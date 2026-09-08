using System.Text;

namespace Service.Api.Services;

/// <summary>Builds safe, readable download file names from user-entered names.</summary>
internal static class FileNames
{
    private const int MaxLength = 50;

    /// <summary>Lower-cases <paramref name="name"/> and replaces whitespace, control characters and the
    /// characters that are invalid in file names on any common platform with "-", collapsing repeats and
    /// trimming the result. Falls back to <paramref name="fallback"/> (typically the entity's id) when nothing
    /// usable remains.</summary>
    public static string Sanitize(string name, string fallback)
    {
        var builder = new StringBuilder(name.Length);
        var lastWasDash = false;

        foreach (var c in name.Trim().ToLowerInvariant())
        {
            // An explicit set rather than Path.GetInvalidFileNameChars(): that list depends on the host OS
            // (only '\0' and '/' on Linux), and the file name must be the same wherever the Service runs.
            var isDash = char.IsWhiteSpace(c) || char.IsControl(c) || c is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|';
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
