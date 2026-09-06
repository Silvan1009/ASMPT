# Build context: the repository root, because NSwag generates the API client from
# Service/Service.Api/OpenApi/Service.Api.json during the build (see compose.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY UI/UI.Web/UI.Web.csproj UI/UI.Web/
COPY Service/Service.Api/OpenApi/Service.Api.json Service/Service.Api/OpenApi/
RUN dotnet restore UI/UI.Web/UI.Web.csproj
COPY UI/ UI/
# No --no-restore here: the SDK only requests the Microsoft.AspNetCore.App.Internal.Assets package (blazor.web.js)
# once the .razor files are present, so the publish must be allowed to complete the restore.
RUN dotnet publish UI/UI.Web/UI.Web.csproj -c Release -o /app \
    && test -f /app/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Data Protection keys (they encrypt the authentication cookie) are persisted here; compose mounts a volume.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
USER app
# HTTPS (port 8081) is enabled through environment variables and the mounted development certificate, see compose.yaml.
ENTRYPOINT ["dotnet", "UI.Web.dll"]
