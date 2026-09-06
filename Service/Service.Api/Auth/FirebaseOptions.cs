using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Service.Api.Auth;

/// <summary>
/// Firebase project settings used to validate ID tokens. Bound from the "Firebase" configuration section.
/// </summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>The Firebase project id. ID tokens carry it as audience and in the issuer.</summary>
    [Required]
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// Host:port of the Firebase Auth emulator (e.g. "localhost:9099"). When set, token validation switches to
    /// emulator mode, which accepts the emulator's unsigned tokens. Only allowed in Development.
    /// The Service never talks to the emulator itself; here the value is only a switch.
    /// </summary>
    public string? EmulatorHost { get; set; }

    public bool UseEmulator => !string.IsNullOrWhiteSpace(EmulatorHost);

    /// <summary>Issuer of Firebase ID tokens; in production also the OIDC authority used to fetch signing keys.</summary>
    public string Issuer => $"https://securetoken.google.com/{ProjectId}";
}

/// <summary>
/// Rejects emulator mode outside Development. Emulator mode skips signature validation and must never
/// reach production.
/// </summary>
public sealed class FirebaseOptionsValidator(IHostEnvironment environment) : IValidateOptions<FirebaseOptions>
{
    public ValidateOptionsResult Validate(string? name, FirebaseOptions options) =>
        options.UseEmulator && !environment.IsDevelopment()
            ? ValidateOptionsResult.Fail(
                "Firebase:EmulatorHost is set outside the Development environment. Emulator mode accepts unsigned tokens and must never be enabled in production.")
            : ValidateOptionsResult.Success;
}
