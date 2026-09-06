# ASMPT

Two independent solutions:

- **Service** — an ASP.NET Core Web API (`Service/Service.slnx` → `Service.Api`) exposing a single
  `GET /api/Echo?message=...` endpoint, with an OpenAPI document and a browsable Swagger UI.
- **UI** — an empty Blazor Server app (`UI/UI.slnx` → `UI.Web`) that talks to the Service through a
  typed C# client generated at build time from the Service's OpenAPI document.

## How the client generation works

1. `Service.Api.csproj` uses `Microsoft.Extensions.ApiDescription.Server` to write the app's OpenAPI
   document to `Service/Service.Api/OpenApi/Service.Api.json` on every build. This file is committed
   to the repo — it's the contract the UI builds against.
2. `UI.Web.csproj` uses `NSwag.ApiDescription.Client` (an `<OpenApiReference>` pointing at that JSON
   file) to generate `ServiceApiClient` / `IServiceApiClient` into `obj/ServiceApiClient.g.cs` on
   every build. Nothing generated is committed on the UI side.

Because generation reads a committed file, **the UI builds without the Service running**. When you
change the Service's API surface, rebuild in this order so the client picks up the change:

```bash
dotnet build Service/Service.slnx   # refreshes Service/Service.Api/OpenApi/Service.Api.json
dotnet build UI/UI.slnx             # regenerates ServiceApiClient from that file
```

If you forget and only rebuild the UI, it will keep using the last-committed API shape — commit the
regenerated JSON alongside your Service changes.

## Running

```bash
dotnet run --project Service/Service.Api   # https://localhost:7168 — Swagger UI at /swagger
dotnet run --project UI/UI.Web             # https://localhost:7176 — see /echo
```

The UI reads the Service's base URL from `UI/UI.Web/appsettings.json` (`ServiceApi:BaseUrl`,
defaults to `https://localhost:7168` to match the Service's default launch profile).

## Note on OpenAPI version

The Service pins its *runtime-served* document (`/openapi/v1.json`, and Swagger UI) to OpenAPI 3.0
for broad tool compatibility. The *build-time* exporter that writes the committed
`OpenApi/Service.Api.json` currently ignores that pin and always writes OpenAPI 3.1 — a known
quirk in `Microsoft.Extensions.ApiDescription.Server` 10.0.11's build-time generator. This has no
practical effect: NSwag reads either version fine, and the generated client is unaffected.
