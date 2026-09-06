using UI.Web.ApiClient;
using UI.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Typed HTTP client for the generated Service API client (see UI.Web.csproj's
// <OpenApiReference> for how ServiceApiClient is generated from the Service's OpenAPI document).
builder.Services.AddHttpClient<IServiceApiClient, ServiceApiClient>(client =>
{
    var baseUrl = builder.Configuration["ServiceApi:BaseUrl"]
        ?? throw new InvalidOperationException("Configuration value 'ServiceApi:BaseUrl' is missing.");
    client.BaseAddress = new Uri(baseUrl);
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
