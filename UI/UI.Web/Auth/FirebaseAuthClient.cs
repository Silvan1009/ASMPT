using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace UI.Web.Auth;

public enum SignInError
{
    InvalidCredentials,
    UserDisabled,
    TooManyAttempts,
    Unavailable,
}

/// <summary>Tokens and profile data returned by Firebase for a signed-in user.</summary>
public sealed record FirebaseSession(
    string Uid,
    string? Email,
    string? DisplayName,
    string IdToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public sealed record SignInResult(FirebaseSession? Session, SignInError? Error);

/// <summary>
/// Thrown when Firebase definitively rejects the session (refresh token revoked, user disabled or deleted).
/// </summary>
public sealed class SessionInvalidException(string message) : Exception(message);

public interface IFirebaseAuthClient
{
    Task<SignInResult> SignInWithPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for a fresh ID token. Throws <see cref="SessionInvalidException"/> when the
    /// session is definitively invalid and <see cref="HttpRequestException"/> for transient failures.
    /// </summary>
    Task<FirebaseSession> RefreshIdTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>Firebase Authentication REST client (Identity Toolkit and Secure Token APIs).</summary>
public sealed class FirebaseAuthClient(
    HttpClient http,
    IOptions<FirebaseOptions> options,
    TimeProvider clock,
    ILogger<FirebaseAuthClient> logger) : IFirebaseAuthClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] SessionInvalidCodes =
    [
        "TOKEN_EXPIRED",
        "USER_DISABLED",
        "USER_NOT_FOUND",
        "INVALID_REFRESH_TOKEN",
        "INVALID_GRANT_TYPE",
        "MISSING_REFRESH_TOKEN",
    ];

    public async Task<SignInResult> SignInWithPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        try
        {
            using var response = await http.PostAsJsonAsync(
                $"{settings.IdentityToolkitBaseUrl}/accounts:signInWithPassword?key={Uri.EscapeDataString(settings.ApiKey)}",
                new SignInRequest(email, password, true),
                Json,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<SignInResponse>(Json, cancellationToken)
                    ?? throw new InvalidOperationException("Firebase returned an empty sign-in response.");
                return new SignInResult(
                    ToSession(body.LocalId, body.Email, body.DisplayName, body.IdToken, body.RefreshToken, body.ExpiresIn),
                    null);
            }

            var code = await ReadErrorCodeAsync(response, cancellationToken);
            // Only the error code is logged; never the email, the password or any token.
            logger.LogInformation("Firebase sign-in rejected with {ErrorCode}", code);
            return new SignInResult(null, code switch
            {
                "USER_DISABLED" => SignInError.UserDisabled,
                "TOO_MANY_ATTEMPTS_TRY_LATER" => SignInError.TooManyAttempts,
                // Unknown email and wrong password are deliberately indistinguishable for the caller.
                "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" or "INVALID_LOGIN_CREDENTIALS" or "INVALID_EMAIL" or "MISSING_PASSWORD"
                    => SignInError.InvalidCredentials,
                _ => SignInError.Unavailable,
            });
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Firebase sign-in endpoint unreachable");
            return new SignInResult(null, SignInError.Unavailable);
        }
    }

    public async Task<FirebaseSession> RefreshIdTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        using var body = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
        ]);
        using var response = await http.PostAsync(
            $"{settings.SecureTokenBaseUrl}/token?key={Uri.EscapeDataString(settings.ApiKey)}",
            body,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var refreshed = await response.Content.ReadFromJsonAsync<RefreshResponse>(Json, cancellationToken)
                ?? throw new InvalidOperationException("Firebase returned an empty refresh response.");
            return ToSession(refreshed.UserId, null, null, refreshed.IdToken, refreshed.RefreshToken, refreshed.ExpiresIn);
        }

        var code = await ReadErrorCodeAsync(response, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest && SessionInvalidCodes.Contains(code))
        {
            throw new SessionInvalidException($"Firebase rejected the refresh token ({code}).");
        }

        throw new HttpRequestException($"Firebase token refresh failed ({(int)response.StatusCode} {code}).");
    }

    private FirebaseSession ToSession(string uid, string? email, string? displayName, string idToken, string refreshToken, string expiresIn) =>
        new(uid, email, displayName, idToken, refreshToken,
            clock.GetUtcNow().AddSeconds(int.Parse(expiresIn, CultureInfo.InvariantCulture)));

    private static async Task<string> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var message = (await response.Content.ReadFromJsonAsync<ErrorEnvelope>(Json, cancellationToken))?.Error?.Message ?? "";
            // Production messages may carry a suffix, e.g. "TOO_MANY_ATTEMPTS_TRY_LATER : Access to this account ...".
            var colon = message.IndexOf(':');
            return (colon > 0 ? message[..colon] : message).Trim();
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private sealed record SignInRequest(string Email, string Password, bool ReturnSecureToken);

    private sealed record SignInResponse(string LocalId, string? Email, string? DisplayName, string IdToken, string RefreshToken, string ExpiresIn);

    private sealed record RefreshResponse(
        [property: JsonPropertyName("id_token")] string IdToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] string ExpiresIn,
        [property: JsonPropertyName("user_id")] string UserId);

    private sealed record ErrorEnvelope(ErrorBody? Error);

    private sealed record ErrorBody(int Code, string? Message);
}
