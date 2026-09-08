using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Service.Api.ErrorHandling;

/// <summary>
/// Maps <see cref="RelatedEntitiesNotFoundException"/> to the same 400 shape ASP.NET Core's automatic model
/// validation uses, so the UI can show the error on the offending field regardless of whether it came from
/// validation or from a service-level check.
/// </summary>
internal sealed class RelatedEntitiesNotFoundExceptionHandler(
    IProblemDetailsService problemDetailsService, ILogger<RelatedEntitiesNotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RelatedEntitiesNotFoundException ex)
        {
            return false;   // not ours: let the next handler (or the default 500 problem) take it
        }

        // ExceptionHandlerMiddleware stays silent for exceptions handled by an IExceptionHandler (its default
        // SuppressDiagnosticsCallback), so this is the only record of the rejection. Ids only, never user text.
        logger.LogWarning("Request rejected: {PropertyName} references unknown ids ({Detail})", ex.PropertyName, ex.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new HttpValidationProblemDetails(new Dictionary<string, string[]> { [ex.PropertyName] = [ex.Message] })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
        });
    }
}
