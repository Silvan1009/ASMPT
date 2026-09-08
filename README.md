# ASMPT

Two independent solutions:

- **Service** — an ASP.NET Core Web API (`Service/Service.slnx` → `Service.Api`) exposing `GET /api/Echo`,
  the `Users` endpoints, and CRUD + search for `Orders`, `Boards` and `Components` (see
  [Production data](#production-data-orders-boards-components)), with an OpenAPI document and a browsable
  Swagger UI. Every endpoint requires a Firebase ID token.
- **UI** — a Blazor Web App (`UI/UI.slnx` → `UI.Web`) with a Firebase-backed login, a cookie session, an
  admin-only Users page, and a MudBlazor-based `/production` page for managing orders, boards and
  components. It talks to the Service through a typed C# client generated at build time from the
  Service's OpenAPI document.

## How the client generation works

1. `Service.Api.csproj` uses `Microsoft.Extensions.ApiDescription.Server` to write the app's OpenAPI
   document to `Service/Service.Api/OpenApi/Service.Api.json` on every build. This file is committed
   to the repo — it's the contract the UI builds against.
2. `UI.Web.csproj` uses `NSwag.ApiDescription.Client` (an `<OpenApiReference>` pointing at that JSON
   file) to generate `ServiceApiClient` / `IServiceApiClient` into `obj/ServiceApiClient.g.cs` on
   every build. Nothing generated is committed on the UI side. The hand-written half of the client,
   `UI/UI.Web/ApiClient/ServiceApiClient.cs`, attaches the caller's Firebase ID token to every request.

Because generation reads a committed file, **the UI builds without the Service running**. When you
change the Service's API surface, rebuild in this order so the client picks up the change:

```bash
dotnet build Service/Service.slnx   # refreshes Service/Service.Api/OpenApi/Service.Api.json
dotnet build UI/UI.slnx             # regenerates ServiceApiClient from that file
```

If you forget and only rebuild the UI, it will keep using the last-committed API shape — commit the
regenerated JSON alongside your Service changes.

The build-time exporter runs the Service's entry point. `Program.cs` therefore skips start-up
configuration validation when the entry assembly is a design-time tool (the exporter, `dotnet ef`) and
enforces it on real starts only.

The `<OpenApiReference>` in `UI.Web.csproj` passes NSwag the option `/DateType:System.DateOnly`, so a
`date`-formatted OpenAPI property (e.g. `Order.orderDate`) generates as `System.DateOnly` in the client
instead of NSwag's default `DateTimeOffset`; `date-time`-formatted properties (e.g. `User.createdAt`) are
unaffected.

## Running

Prerequisites: the .NET 10 SDK, Docker, and a trusted HTTPS development certificate
(`dotnet dev-certs https --trust`). The UI's session cookie is marked `Secure`, so the browser must
reach the UI over HTTPS in either mode.

**Everything in Docker**

```bash
dotnet dev-certs https -ep certs/aspnetapp.pfx -p changeit   # once: the UI container serves HTTPS with your dev certificate
docker compose up -d --build
```

| What | URL |
|---|---|
| UI | https://localhost:7176 (HTTPS only; http://localhost:5131 redirects there) |
| Service (Swagger UI) | http://localhost:5073/swagger |
| Firebase Emulator UI | http://localhost:4000/auth |
| PostgreSQL | localhost:5432 (`postgres` / `postgres`, database `asmpt`) |

Type the scheme: `localhost:7176` without `https://` sends plain HTTP to the TLS port and the browser
shows `ERR_EMPTY_RESPONSE`. The Service container applies pending EF Core migrations at start-up
(`Database:MigrateOnStartup`, set only in `compose.yaml`). Inside the compose network the UI calls the Service over plain HTTP and both
apps use the emulator at `firebase-auth:9099`; the host-facing URLs above use the same dev certificate
as `dotnet run`. `certs/` is git-ignored; the certificate password can be changed through the
`DEV_CERT_PASSWORD` variable, for example in a `.env` file.

**Apps on the host, infrastructure in Docker**

```bash
docker compose up -d --build firebase-auth postgres       # Firebase Auth emulator (:9099, UI on :4000) and PostgreSQL (:5432)
dotnet ef database update --project Service/Service.Api   # creates the Users table
dotnet run --project Service/Service.Api   # https://localhost:7168 — Swagger UI at /swagger
dotnet run --project UI/UI.Web             # https://localhost:7176
```

Both projects list the `https` launch profile first, so `dotnet run` uses it. Stop the app containers
first (`docker compose stop ui service`) because the UI uses port 7176 in both modes.

In both modes, data lives in named volumes: `docker compose down` keeps it and `docker compose down -v`
starts over (the emulator re-imports its seed, the migrations are re-applied). The containers use
`restart: unless-stopped`, so they come back after a Docker or machine restart until you stop them
yourself with `docker compose stop` or `down`. Log in with the emulator's
seed account `dev@example.com` / `Password1!` (custom claim `role=admin`). More users can be created in
the Emulator UI at http://localhost:4000/auth; see `docker/firebase-auth-emulator/README.md` for the
emulator itself.

The UI reads the Service's base URL from `UI/UI.Web/appsettings.json` (`ServiceApi:BaseUrl`, defaults to
`https://localhost:7168`).

## Authentication

Firebase Authentication is the identity provider. The apps forward passwords to Firebase during login
and never store credentials themselves.

**UI (`UI/UI.Web`)**

- `/login` (`Components/Pages/Login.razor`) is a statically rendered page. On submit the server calls the
  Firebase Identity Toolkit REST API (`accounts:signInWithPassword`) through `Auth/FirebaseAuthClient.cs`
  and, on success, issues an encrypted `HttpOnly`, `Secure`, `SameSite=Lax` cookie (`ASMPT.Auth`, sliding
  14 days, persistent only with "Remember me"). An unknown email and a wrong password produce the same
  message.
- The cookie principal carries the Firebase uid, email, name, the `role` custom claim as an ASP.NET Core
  role, and the Firebase refresh token as a private claim (`Auth/FirebasePrincipalFactory.cs`). Interactive
  Blazor circuits only see the principal, which is why the refresh token lives there; the ID token never
  enters the cookie.
- Calls to the Service carry a Firebase ID token supplied by `Auth/UserTokenProvider.cs`: tokens are cached
  per user in memory (`Auth/IdTokenCache.cs`) and refreshed through the Secure Token API shortly before
  they expire.
- `Auth/FirebaseCookieEvents.cs` validates the session on every request. A definitive refresh failure
  (user disabled or deleted, token revoked) signs the user out; transient failures keep the session.
- Every routable component requires a signed-in user (`@attribute [Authorize]` in
  `Components/_Imports.razor`); `/login`, `/access-denied`, `/Error` and `/not-found` opt out.
  `/users` requires the `Admin` policy (role `admin`). Logout is a POST to `/logout` from the layout's
  antiforgery-protected form.

**Service (`Service/Service.Api`)**

- `Auth/ConfigureJwtBearerOptions.cs` validates Firebase ID tokens as JWT bearer tokens. In production the
  signing keys come from the project's OIDC discovery document (`https://securetoken.google.com/<projectId>`);
  in emulator mode signature validation is skipped because the emulator issues unsigned tokens. Emulator
  mode is refused outside `Development` (`Auth/FirebaseOptions.cs`).
- Authorization is on by default (fallback policy: authenticated user); `GET /api/Users` additionally
  requires the `Admin` policy. 401/403 responses are RFC 7807 problem details. Swagger UI offers an
  "Authorize" button that takes a raw ID token.
- `PUT /api/Users/me` creates or refreshes the caller's row in the `Users` table from the token claims
  (`Services/UserService.cs`); the UI calls it after every login. `GET /api/Users` lists the table.

**Configuration**

| Key | Service | UI |
|---|---|---|
| `Firebase:ProjectId` | required | required |
| `Firebase:ApiKey` | — | required (the Firebase Web API key: not a secret, but environment-specific) |
| `Firebase:EmulatorHost` | optional, `Development` only | optional, `Development` only |

`appsettings.Development.json` in both projects points at the emulator (`demo-asmpt`, `localhost:9099`).
For other environments supply the values through environment variables (`Firebase__ProjectId`,
`Firebase__ApiKey`) or user secrets (`dotnet user-secrets set Firebase:ApiKey <key> --project UI/UI.Web`);
empty values fail at start-up. Roles are Firebase custom claims (`role`) set through the Admin SDK or the
Emulator UI, not in the apps.

## UI rendering and MudBlazor

The UI uses [MudBlazor](https://mudblazor.com) for its component library and renders interactively
(`InteractiveServer`) everywhere by default, wired up in `Components/App.razor`:

```razor
private IComponentRenderMode? PageRenderMode => HttpContext.AcceptsInteractiveRouting() ? InteractiveServer : null;
```

Pages that need a classic request/response cycle — `/login` (reads `HttpContext` as a cascading parameter
and posts a form) and `/Error` — opt out with `@attribute [ExcludeFromInteractiveRouting]` and stay
statically rendered; navigating to them forces a full-page reload. The layout's own logout button is a
plain HTML `<form>` (not an `EditForm`) for the same reason: only a full POST can clear the auth cookie
from an interactive circuit, and its `<AntiforgeryToken />` still works because ASP.NET Core persists the
antiforgery token from the initial prerender into the circuit's component state.

## Production data (Orders, Boards, Components)

The Service exposes CRUD, search and bulk delete for three entities with two many-to-many relationships:
an `Order` includes one or more `Board`s (and a board can be in several orders), and a `Board` carries one
or more `Component`s (and a component can be placed on several boards). Each family has its own repository,
service and controller (`OrdersController`/`BoardsController`/`ComponentsController`), following the same
layering as `Users`.

- `GET api/{orders|boards|components}?search=` — case-insensitive name/description search (`ILIKE`); lists
  everything when `search` is omitted.
- `GET/POST/PUT/DELETE api/{orders|boards|components}[/{id}]` — standard CRUD. `POST`/`PUT` validate the
  payload (`[ApiController]`'s automatic model validation) and, for orders/boards, that every referenced
  board/component id exists; both failure kinds come back as a 400 `HttpValidationProblemDetails` with the
  errors keyed by property name (e.g. `errors.BoardIds`).
- `POST api/{orders|boards|components}/batch-delete` — deletes several rows at once (body
  `{ "ids": [...] }`); unknown ids are ignored. Deleting a board or component removes it from any order or
  board that referenced it (the join tables' foreign keys cascade); it does not delete the order/board
  itself, even if that leaves it with no boards/components.
- `GET api/orders/{id}/download` — the order download required by the spec: a JSON file (the order, its
  boards, and each board's components) simulating the hand-off to a production line, served as
  `application/octet-stream` with a `Content-Disposition: attachment` file name.

In the UI, `/production` (`Components/Pages/Production.razor`) hosts one MudBlazor grid per entity
(`Components/Production/{Orders,Boards,Components}Grid.razor`) inside a `MudTabs`, each with search,
create/edit dialogs (`{Order,Board,Component}Dialog.razor`), single and multi-row delete, and — for
orders — the download button. The multi-select fields (an order's boards, a board's components) require
at least one entry, matching the "one or more" requirement.

## Data access and testability

The Service is wired up for EF Core against PostgreSQL, structured so business logic never talks
to EF Core directly:

- `Data/ApplicationDbContext.cs` — the `DbContext`. Entity mappings live in `Data/Configurations/`
  (`IEntityTypeConfiguration<T>`), entities in `Data/Entities/`: `User`, `Order`, `Board`, `Component`,
  plus the payload-free join entities `OrderBoard` and `BoardComponent` for the many-to-many relationships.
- `Repositories/IRepository.cs` + `Repository.cs` — a generic repository interface over
  `ApplicationDbContext`, registered as an open generic (`IRepository<TEntity>` resolves for any
  entity with no per-entity DI wiring). Business logic depends on `IRepository<T>`, not the
  `DbContext`, so it can be unit-tested against a mock/fake repository. `Order`, `Board` and `Component`
  additionally get their own repository interface (`IOrderRepository` etc., in the same file pattern)
  for the graph-loading and search queries the generic interface can't express (`Include`, `ILike`,
  `ExecuteDeleteAsync`).
- `Services/IEchoService.cs` + `EchoService.cs`, `Services/IUserService.cs` + `UserService.cs`, and the
  equivalent `IOrderService`/`IBoardService`/`IComponentService` — the same pattern applied to every
  feature: controllers depend on the interfaces, not on concrete classes, so the controllers and the
  services can each be unit-tested in isolation.
- Request payloads (`Models/{Order,Board,Component}Request.cs`, `BatchDeleteRequest.cs`) are plain mutable
  classes, not records: ASP.NET Core's record-aware model validation reads `DataAnnotations` from the
  primary constructor's parameters, while the OpenAPI schema generator reads them from properties: a
  `[property: ...]`-targeted attribute (needed to satisfy the latter) makes the former throw
  `InvalidOperationException` at request time. An ordinary class has no such ambiguity.

Connection string lives in `appsettings.json` under `ConnectionStrings:DefaultConnection`, pointing
at a local Postgres instance (`Host=localhost;Port=5432;Database=asmpt;Username=postgres;Password=postgres`)
— update it for your environment, or override it via `appsettings.Development.json` / user secrets.

Migrations are committed under `Data/Migrations/`. Once entities change:

```bash
dotnet ef migrations add <Name> --project Service/Service.Api --output-dir Data/Migrations
dotnet ef database update --project Service/Service.Api   # requires a reachable Postgres instance
```

## Logging and error tracing

Both apps log through [Serilog](https://serilog.net) instead of the default Microsoft console logger,
configured entirely from the `Serilog` section of `appsettings.json` (the `Logging` section is unused once
Serilog is added). Every log line carries the current [W3C trace id](https://www.w3.org/TR/trace-context/) as
its `{TraceId}` column — the same id ASP.NET Core already writes into every RFC 7807 problem response as
`traceId`, and the value shown to the user as an "error reference" (in the `/production` grid and dialog
alerts, on the Echo page, in the layout's error banner, and on `/Error`). The UI forwards the W3C `traceparent`
header on every call to the Service — `Logging/TraceCircuitHandler.cs` gives each Blazor circuit interaction
its own trace, since SignalR would otherwise clear it on every hub invocation — so one reference finds the
matching lines in **both** apps' log files.

**Where the files are.** The path in `appsettings.json` (`logs/service-api-.log`, `logs/ui-web-.log`) is
relative to the process's working directory, not the project folder:

| Run mode | Service | UI |
|---|---|---|
| `dotnet run` | `Service/Service.Api/logs/` | `UI/UI.Web/logs/` |
| `docker compose up` | `./logs/service/` on the host (bind-mounted) | `./logs/ui/` on the host (bind-mounted) |

Both locations are git-ignored. `dotnet build` and `dotnet ef` never create a `logs/` directory: the Service
skips `AddSerilog` when it detects it is running under design-time tooling (the same
`isRunningUnderTooling` check `Program.cs` already uses for start-up validation), so a build or a migration
never writes log files into the source tree.

**Format.** Console output is a short human-readable line; the file adds full timestamps and
machine-readable `{Properties:j}` — whatever isn't already in the message or template, e.g. `UserId`,
`CircuitId`, `RequestId` — and rolls daily or every 50 MB, keeping the last 31 files. Example file line
(a failed request, both apps write this shape):

```
[2026-09-08 14:32:07.114 +00:00] INF a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6 Serilog.AspNetCore.RequestLoggingMiddleware: HTTP POST /api/Orders responded 400 in 12.3401 ms {"UserId":"KJ3n...","RequestId":"0HN..."}
```

To find every line for one failure, search both apps' files for the trace id shown to the user:

```powershell
Select-String -Path UI/UI.Web/logs/*.log, Service/Service.Api/logs/*.log -Pattern <trace-id>
```

**What is never logged.** Names, descriptions, search terms and any other user-entered text; email addresses
(users are identified by their Firebase uid instead); passwords, Firebase ID tokens and refresh tokens. SQL
parameter values (`EnableSensitiveDataLogging`) are logged only in `Development`, which includes the Docker
Compose stack — it always runs with `ASPNETCORE_ENVIRONMENT=Development`.

**Configuration knobs**, overridable through environment variables (`__` separates segments, as with any
other `IConfiguration` key) or `appsettings.Development.json`:

| Key | Purpose |
|---|---|
| `Serilog__MinimumLevel__Default` | Baseline level (`Information`) |
| `Serilog__MinimumLevel__Override__<category>` | Per-category override, e.g. `Microsoft.EntityFrameworkCore.Database.Command` |
| `Serilog__WriteTo__FileSink__Args__configure__0__Args__path` | The rolling file's path/prefix |
| `Serilog__WriteTo__FileSink__Args__blockWhenFull` | `true` blocks the app rather than dropping events when the file sink falls behind (default `false`: availability over completeness) |

**Adding a central sink** (Seq, an OTLP collector, etc.) later needs no code change: add the sink's NuGet
package, list its assembly under `Serilog:Using`, and add a keyed entry under `Serilog:WriteTo` (see the
existing `ConsoleSink`/`FileSink` entries for the shape). **Switching the file to**
[CLEF](https://clef-json.org/) (for a log shipper that expects JSON) means *replacing* the File sink's
`outputTemplate` with a `formatter`:

```json
"formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
```

Setting both is a trap: Serilog.Settings.Configuration resolves the method overload with the most matching
argument names, `outputTemplate` wins silently, and `formatter` is ignored.

## Note on OpenAPI version

The Service pins its *runtime-served* document (`/openapi/v1.json`, and Swagger UI) to OpenAPI 3.0
for broad tool compatibility. The *build-time* exporter that writes the committed
`OpenApi/Service.Api.json` currently ignores that pin and always writes OpenAPI 3.1 — a known
quirk in `Microsoft.Extensions.ApiDescription.Server` 10.0.11's build-time generator. This has no
practical effect: NSwag reads either version fine, and the generated client is unaffected.
