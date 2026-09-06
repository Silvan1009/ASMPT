using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using UI.Web.ApiClient;
using UI.Web.Auth;
using UI.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMudServices();

// Authentication: the login page signs the user in with Firebase (REST) and issues this cookie.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ASMPT.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.EventsType = typeof(FirebaseCookieEvents);
    });
builder.Services.AddScoped<FirebaseCookieEvents>();

// Authorization: pages are protected by [Authorize] in Components/_Imports.razor. No fallback policy here,
// because it would also cover static assets and the Blazor endpoints.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(FirebaseRoles.Admin));

// Firebase REST sign-in and ID-token handling.
builder.Services.AddOptions<FirebaseOptions>()
    .BindConfiguration(FirebaseOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<FirebaseOptions>, FirebaseOptionsValidator>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IIdTokenCache, IdTokenCache>();
builder.Services.AddScoped<IFirebaseIdTokenService, FirebaseIdTokenService>();
builder.Services.AddScoped<IUserTokenProvider, UserTokenProvider>();
builder.Services.AddHttpClient<IFirebaseAuthClient, FirebaseAuthClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Typed HTTP client for the generated Service API client (see UI.Web.csproj's <OpenApiReference> for how
// ServiceApiClient is generated; ApiClient/ServiceApiClient.cs attaches the user's Firebase ID token).
builder.Services.AddHttpClient<IServiceApiClient, ServiceApiClient>(client =>
{
    var baseUrl = builder.Configuration["ServiceApi:BaseUrl"]
        ?? throw new InvalidOperationException("Configuration value 'ServiceApi:BaseUrl' is missing.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();   // after authentication: antiforgery tokens are bound to the identity

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Logout is a POST from the layout's form; the [FromForm] parameter makes antiforgery validation mandatory.
app.MapPost("/logout", async (HttpContext httpContext, [FromForm] string? returnUrl, IIdTokenCache tokenCache) =>
{
    if (httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } uid)
    {
        tokenCache.Remove(uid);
    }

    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var target = LocalUrl.IsLocal(returnUrl) ? $"/login?returnUrl={Uri.EscapeDataString(returnUrl!)}" : "/login";
    return TypedResults.LocalRedirect(target);
}).RequireAuthorization();

app.Run();
