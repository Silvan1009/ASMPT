using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Debugging;
using Service.Api.Auth;
using Service.Api.Data;
using Service.Api.ErrorHandling;
using Service.Api.Logging;
using Service.Api.Repositories;
using Service.Api.Services;

// Console-only bootstrap logger: records failures that happen before the configured (Serilog section-driven)
// logger exists, e.g. a Build() failure. SelfLog surfaces Serilog's own problems (e.g. an unwritable logs/
// directory) on stderr, since Serilog cannot log its own failures through itself.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
SelfLog.Enable(Console.Error);

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Design-time tools (the build-time OpenAPI export, dotnet ef) run this entry point without the app's real
    // environment and configuration. Start-up validation is enforced for real starts only.
    var isRunningUnderTooling = Assembly.GetEntryAssembly() != typeof(Program).Assembly;

    // Add services to the container.

    if (!isRunningUnderTooling)
    {
        // Replaces ILoggerFactory; the "Serilog" appsettings section governs logging from here on (the
        // "Logging" section is ignored). Skipped under tooling so dotnet build / dotnet ef never create
        // logs/ in the source tree (EF logs at Information during migrations).
        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services));
    }

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    // Maps RelatedEntitiesNotFoundException (an order/board referencing an unknown related id) to a 400
    // validation problem; consulted by the app.UseExceptionHandler() call below.
    builder.Services.AddExceptionHandler<RelatedEntitiesNotFoundExceptionHandler>();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options =>
    {
        // Pin to OpenAPI 3.0 for broad tooling compatibility (e.g. NSwag client generation).
        options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    });

    // Authentication: Firebase ID tokens as JWT bearer tokens (see Auth/ConfigureJwtBearerOptions.cs).
    var firebaseOptions = builder.Services.AddOptions<FirebaseOptions>()
        .BindConfiguration(FirebaseOptions.SectionName)
        .ValidateDataAnnotations();
    if (!isRunningUnderTooling)
    {
        firebaseOptions.ValidateOnStart();
    }
    builder.Services.AddSingleton<IValidateOptions<FirebaseOptions>, FirebaseOptionsValidator>();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
    builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();

    // Authorization: every endpoint requires an authenticated user unless marked [AllowAnonymous];
    // the "Admin" policy maps to the Firebase custom claim role=admin.
    builder.Services.AddAuthorizationBuilder()
        .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
        .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(FirebaseRoles.Admin));

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
        if (builder.Environment.IsDevelopment())
        {
            // Parameter values in the SQL log, and full EF Core exception detail. Local development only:
            // the compose stack also runs with ASPNETCORE_ENVIRONMENT=Development, so its logs/ carry this too.
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });

    // Generic repository; IRepository<TEntity> resolves for any entity registered on ApplicationDbContext.
    builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IBoardRepository, BoardRepository>();
    builder.Services.AddScoped<IComponentRepository, ComponentRepository>();

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<IEchoService, EchoService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<IBoardService, BoardService>();
    builder.Services.AddScoped<IComponentService, ComponentService>();

    var app = builder.Build();

    // Opt-in for the local Docker stack (compose.yaml): apply pending migrations at start-up. Off by default,
    // because production databases are migrated as a deliberate deployment step.
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    // Configure the HTTP request pipeline.
    if (!isRunningUnderTooling)
    {
        // Outermost: sees the final status code of every request, including one an exception handler
        // downgrades from 500 to a mapped code (e.g. RelatedEntitiesNotFoundException -> 400). See
        // Logging/RequestLogging.cs. Guarded like AddSerilog above: its DiagnosticContext dependency is only
        // registered there, and unlike dotnet ef (which aborts at Build() via HostAbortedException), the
        // OpenAPI exporter runs the host to completion, so this middleware would otherwise be constructed
        // with nothing to resolve.
        app.UseSerilogRequestLogging(RequestLogging.Configure);
    }

    app.UseExceptionHandler();   // RFC 7807 bodies for unhandled exceptions
    app.UseStatusCodePages();    // RFC 7807 bodies for the otherwise empty 401/403 responses

    if (app.Environment.IsDevelopment())
    {
        // MapOpenApi creates an endpoint, which the fallback policy would otherwise protect.
        app.MapOpenApi().AllowAnonymous();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Service API v1");
        });
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseMiddleware<UserLogContextMiddleware>();   // UserId on every event written for the rest of the request
    app.UseAuthorization();

    app.MapControllers();

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is how design-time tools (the OpenAPI exporter, dotnet ef) stop the host after
    // Build(); it must propagate untouched, not be logged as a failure.
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();   // flushes the Async file sink's buffer
}
