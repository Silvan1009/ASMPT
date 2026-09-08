# Build context: the repository root (see compose.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Service/Service.Api/Service.Api.csproj Service/Service.Api/
RUN dotnet restore Service/Service.Api/Service.Api.csproj
COPY Service/ Service/
# The committed OpenAPI document is the contract; the build-time export is not needed inside the image build.
RUN dotnet publish Service/Service.Api/Service.Api.csproj -c Release -o /app --no-restore \
    -p:OpenApiGenerateDocumentsOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Npgsql probes GSSAPI on its first connection and logs "Cannot load library libgssapi_krb5.so.2" without this package.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# The Serilog file sink writes to /app/logs (relative "logs/" in appsettings.json resolves against the
# working directory, /app); /app itself is root-owned, so the directory must be created and chowned here.
RUN mkdir -p /app/logs && chown app:app /app/logs
USER app
# Listens on http://+:8080 (the image default). Configuration comes from environment variables, see compose.yaml.
ENTRYPOINT ["dotnet", "Service.Api.dll"]
