using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using Serilog;
using Serilog.Debugging;
using UI.Web.ApiClient;
using UI.Web.Auth;
using UI.Web.Components;
using UI.Web.Logging;

// Console-only bootstrap logger: records failures that happen before the configured (Serilog section-driven)
// logger exists, e.g. a Build() failure. SelfLog surfaces Serilog's own problems (e.g. an unwritable logs/
// directory) on stderr, since Serilog cannot log its own failures through itself.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
SelfLog.Enable(Console.Error);

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Replaces ILoggerFactory; the "Serilog" appsettings section governs logging from here on (the "Logging"
    // section is ignored). Unlike the Service, nothing runs this Program under design-time tooling.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    // One trace id per user interaction inside a circuit; see Logging/TraceCircuitHandler.cs.
    builder.Services.AddScoped<CircuitHandler, TraceCircuitHandler>();
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
    // Outermost: sees the final status code of every request. See Logging/RequestLogging.cs.
    app.UseSerilogRequestLogging(RequestLogging.Configure);
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseMiddleware<UserLogContextMiddleware>();   // UserId for the request and, via /_blazor, the whole circuit
    app.UseAuthorization();
    app.UseAntiforgery();   // after authentication: antiforgery tokens are bound to the identity

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Logout is a POST from the layout's form. It stays anonymous on purpose: an expired or rejected session has
    // nothing to sign out of (and its antiforgery token no longer matches the now-anonymous request), so the post
    // simply ends on the login page. RequireAuthorization would instead bounce it to /login?ReturnUrl=/logout, a
    // page that does not exist. For a live session the antiforgery token is validated by hand.
    app.MapPost("/logout", async (HttpContext httpContext, [FromForm] string? returnUrl, IAntiforgery antiforgery, IIdTokenCache tokenCache) =>
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(httpContext);
            }
            catch (AntiforgeryValidationException ex)
            {
                app.Logger.LogWarning(ex, "Logout rejected: antiforgery validation failed for user {Uid}",
                    httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));
                return Results.BadRequest();
            }

            if (httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } uid)
            {
                tokenCache.Remove(uid);
            }

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        var target = LocalUrl.IsLocal(returnUrl) ? $"/login?returnUrl={Uri.EscapeDataString(returnUrl!)}" : "/login";
        return Results.LocalRedirect(target);
    }).DisableAntiforgery();

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();   // flushes the Async file sink's buffer
}
