using UI.Web.Auth;

namespace UI.Web.ApiClient;

/// <summary>
/// Shared helpers for turning a failed <see cref="IServiceApiClient"/> call into something a page can show.
/// An interactive circuit cannot clear the auth cookie itself (see <see cref="IsSessionExpired"/> callers),
/// so pages surface a "reload" hint instead of redirecting.
/// </summary>
internal static class ApiErrors
{
    /// <summary>True for a 401 response or an already-known-invalid session (see
    /// <see cref="SessionInvalidException"/>), matching the pattern used by <c>Echo.razor</c>.</summary>
    public static bool IsSessionExpired(Exception ex)
        => ex is SessionInvalidException or ApiException { StatusCode: 401 };

    /// <summary>True when this is a failure the caller should handle (session expiry, a problem response,
    /// or a transport failure) rather than let bubble up.</summary>
    public static bool IsApiFailure(Exception ex)
        => ex is ApiException or HttpRequestException or SessionInvalidException;

    /// <summary>Field-level validation errors from a 400 response, keyed by request property name
    /// (case-insensitive lookup is the caller's responsibility). Null when <paramref name="ex"/> did not
    /// carry a validation problem body.
    /// Note: referencing <c>HttpValidationProblemDetails</c> unqualified is unambiguous here because this
    /// file's own namespace (UI.Web.ApiClient, the generated client's namespace) resolves before the Web
    /// SDK's global "using Microsoft.AspNetCore.Http" is consulted; Razor files in other namespaces need the
    /// "ValidationProblemDetails" alias declared in _Imports.razor instead.</summary>
    public static IDictionary<string, ICollection<string>>? TryGetFieldErrors(Exception ex)
        => ex is ApiException<HttpValidationProblemDetails> { Result.Errors: { } errors } ? errors : null;

    /// <summary>A short, user-safe message for a failed call that was not a session expiry or a field
    /// validation error.</summary>
    public static string Describe(Exception ex, string fallback) => ex switch
    {
        ApiException<ProblemDetails> { Result.Detail: { Length: > 0 } detail } => detail,
        ApiException { StatusCode: 404 } => "It no longer exists. Reload the page to refresh the list.",
        _ => fallback,
    };
}
