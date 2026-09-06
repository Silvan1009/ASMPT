using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Service.Api.Auth;

/// <summary>
/// Configures the JwtBearer scheme for Firebase ID tokens. Runs lazily when the scheme's options are first
/// materialised (on the first authenticated request), so building the host for the build-time OpenAPI export
/// or for EF Core design-time tooling never touches the Firebase configuration.
/// </summary>
public sealed class ConfigureJwtBearerOptions(IOptions<FirebaseOptions> firebase, IHostEnvironment environment)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var settings = firebase.Value;

        // Keep the token's claim names ("sub", "email", "role", ...) instead of mapping them to the legacy
        // SOAP-style claim types.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.ProjectId,
            NameClaimType = FirebaseClaims.Sub,
            RoleClaimType = FirebaseClaims.Role,
        };

        if (settings.UseEmulator)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Firebase emulator mode is only allowed in the Development environment.");
            }

            // The emulator issues unsigned tokens (alg=none). Only the signature check is skipped; issuer,
            // audience and lifetime are still validated. No Authority is set, so nothing is fetched.
            options.TokenValidationParameters.RequireSignedTokens = false;
            options.TokenValidationParameters.ValidateIssuerSigningKey = false;
        }
        else
        {
            // OIDC discovery document of the Firebase project; signing keys are fetched and cached from there.
            options.Authority = settings.Issuer;
        }
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}
