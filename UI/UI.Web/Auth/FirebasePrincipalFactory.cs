using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace UI.Web.Auth;

/// <summary>Builds the cookie principal for a Firebase session.</summary>
public static class FirebasePrincipalFactory
{
    public static ClaimsPrincipal Create(FirebaseSession session)
    {
        // The ID token came straight from Google (or the emulator) over the server-side channel, so it is
        // only parsed here; Service.Api validates tokens cryptographically on every call.
        using var payload = JwtPayload.Parse(session.IdToken);
        var root = payload.RootElement;

        var email = root.GetStringOrNull("email") ?? session.Email;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.Uid),
            new(ClaimTypes.Name, root.GetStringOrNull("name") ?? session.DisplayName ?? email ?? session.Uid),
            new(FirebaseClaimTypes.RefreshToken, session.RefreshToken),
        };

        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        if (root.GetStringOrNull("role") is { Length: > 0 } role)
        {
            // Firebase custom claim -> ASP.NET Core role: IsInRole, RequireRole and AuthorizeView Roles all read it.
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}

internal static class JwtPayload
{
    public static JsonDocument Parse(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException("The token is not a compact JWS.");
        }

        return JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
    }

    public static string? GetStringOrNull(this JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
