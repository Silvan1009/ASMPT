namespace UI.Web.Auth;

/// <summary>Guards redirect targets so a crafted returnUrl cannot send the user to another site.</summary>
public static class LocalUrl
{
    public static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));

    public static string Sanitize(string? url) => IsLocal(url) ? url! : "/";
}
