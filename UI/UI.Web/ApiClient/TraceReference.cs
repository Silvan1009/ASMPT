using System.Diagnostics;

namespace UI.Web.ApiClient;

/// <summary>
/// The value a user can quote so that the log lines of a failed interaction — in both apps' files — can be
/// found. See Logging/TraceCircuitHandler.cs for how a circuit interaction gets a trace id in the first place.
/// </summary>
internal static class TraceReference
{
    /// <summary>Trace id of the activity in progress: what both apps write as {TraceId} in their log lines.</summary>
    public static string? Current => Activity.Current?.TraceId.ToHexString();

    /// <summary><see cref="Current"/>, or (when no activity was running here) the trace id the Service put into
    /// its problem response — it starts its own trace when no traceparent header reached it.</summary>
    public static string? For(Exception exception) => Current ?? FromProblemDetails(exception);

    private static string? FromProblemDetails(Exception exception)
    {
        var extensions = exception switch
        {
            ApiException<HttpValidationProblemDetails> e => e.Result?.AdditionalProperties,
            ApiException<ProblemDetails> e => e.Result?.AdditionalProperties,
            _ => null,
        };
        if (extensions is null || !extensions.TryGetValue("traceId", out var value) || value?.ToString() is not { Length: > 0 } traceId)
        {
            return null;
        }

        // ASP.NET Core writes the W3C activity id "00-<trace id>-<span id>-<flags>"; only the trace id itself
        // is a useful reference to show.
        var parts = traceId.Split('-');
        return parts.Length == 4 ? parts[1] : traceId;
    }
}
