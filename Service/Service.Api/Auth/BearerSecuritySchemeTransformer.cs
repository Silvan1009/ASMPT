using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Service.Api.Auth;

/// <summary>
/// Adds the HTTP bearer security scheme (Firebase ID token) to the OpenAPI document and requires it for
/// every operation, so Swagger UI offers "Authorize" and the contract documents the requirement.
/// Has no dependencies on purpose: it also runs inside the build-time document export.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Firebase ID token (the idToken returned by accounts:signInWithPassword).",
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document)] = new List<string>(),
        });

        return Task.CompletedTask;
    }
}
