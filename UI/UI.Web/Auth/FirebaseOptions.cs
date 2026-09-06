using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace UI.Web.Auth;

/// <summary>
/// Firebase project settings for the server-side sign-in. Bound from the "Firebase" configuration section.
/// </summary>
public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    [Required]
    public string ProjectId { get; set; } = "";

    /// <summary>
    /// The Firebase Web API key. It identifies the project towards the Identity Toolkit REST API and is not a
    /// secret, but it is environment-specific: keep real values in user secrets or environment variables.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Host:port of the Firebase Auth emulator (e.g. "localhost:9099"). When set, all sign-in and token
    /// requests go to the emulator instead of Google. Only allowed in Development.
    /// </summary>
    public string? EmulatorHost { get; set; }

    public bool UseEmulator => !string.IsNullOrWhiteSpace(EmulatorHost);

    /// <summary>Base URL of the Identity Toolkit API (sign-in), without a trailing slash.</summary>
    public string IdentityToolkitBaseUrl => UseEmulator
        ? $"http://{EmulatorHost}/identitytoolkit.googleapis.com/v1"
        : "https://identitytoolkit.googleapis.com/v1";

    /// <summary>Base URL of the Secure Token API (token refresh), without a trailing slash.</summary>
    public string SecureTokenBaseUrl => UseEmulator
        ? $"http://{EmulatorHost}/securetoken.googleapis.com/v1"
        : "https://securetoken.googleapis.com/v1";
}

/// <summary>
/// Rejects emulator mode outside Development: the emulator speaks plain HTTP and issues unsigned tokens.
/// </summary>
public sealed class FirebaseOptionsValidator(IHostEnvironment environment) : IValidateOptions<FirebaseOptions>
{
    public ValidateOptionsResult Validate(string? name, FirebaseOptions options) =>
        options.UseEmulator && !environment.IsDevelopment()
            ? ValidateOptionsResult.Fail(
                "Firebase:EmulatorHost is set outside the Development environment. The emulator must never be used in production.")
            : ValidateOptionsResult.Success;
}
