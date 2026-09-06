using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Service.Api.Auth;
using Service.Api.Data;
using Service.Api.ErrorHandling;
using Service.Api.Repositories;
using Service.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Design-time tools (the build-time OpenAPI export, dotnet ef) run this entry point without the app's real
// environment and configuration. Start-up validation is enforced for real starts only.
var isRunningUnderTooling = Assembly.GetEntryAssembly() != typeof(Program).Assembly;

// Add services to the container.

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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
app.UseAuthorization();

app.MapControllers();

app.Run();
