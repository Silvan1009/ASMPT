namespace Service.Api.Services;

/// <summary>Normalises user-entered text from request payloads, so every service stores one canonical form.</summary>
internal static class RequestText
{
    /// <summary>A required text field: surrounding whitespace removed.</summary>
    public static string Required(string value) => value.Trim();

    /// <summary>An optional text field: null when blank, otherwise trimmed.</summary>
    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
