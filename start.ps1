<#
.SYNOPSIS
    Starts the whole ASMPT stack in Docker - Firebase Auth emulator, PostgreSQL, Service and UI - on a
    machine that has Docker Desktop and nothing else the stack needs.

.DESCRIPTION
    Safe to re-run: every step looks at the current state first and only does what is missing.

      1. Docker       starts Docker Desktop when the engine is not reachable, and waits for it
      2. Ports        stops early when another program already listens on a port compose.yaml publishes
      3. Certificate  creates certs\aspnetapp.pfx, which the UI container serves HTTPS with, and trusts
                      it for the current Windows user
      4. Containers   docker compose up --build, waiting for the health checks
      5. Apps         waits until the Service and the UI answer, then prints the URLs

    The certificate comes from `dotnet dev-certs`. With a .NET SDK installed (Visual Studio's ASP.NET
    workload brings one) that is the machine's own ASP.NET Core development certificate, the one
    `dotnet run` and Visual Studio use as well. With only the .NET runtime, the same tool runs inside
    the .NET SDK image instead - the image the Dockerfiles build with, so it costs no extra download.

.PARAMETER SkipCertificateTrust
    Leave the Windows certificate store alone. The UI still works, but the browser shows a certificate
    warning. For machines where policy blocks user root certificates, and for unattended runs.

.EXAMPLE
    start.cmd
    Double-click it or run it from a terminal. It calls this script with -ExecutionPolicy Bypass, which
    a fresh Windows installation needs, without changing the machine's execution policy.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\start.ps1 -SkipCertificateTrust
#>
# Keep this file ASCII: Windows PowerShell 5.1 reads scripts without a byte order mark as ANSI. And no line
# of the help above may start with ".NET" - PowerShell takes it for an unknown help keyword and drops the block.
[CmdletBinding()]
param(
    [switch]$SkipCertificateTrust
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_NOLOGO = '1'

$RepoRoot = $PSScriptRoot
# compose.yaml mounts this folder into the UI container as /https.
$CertDir = Join-Path $RepoRoot 'certs'
$PfxPath = Join-Path $CertDir 'aspnetapp.pfx'
# The build stage of Service/Service.Api/Dockerfile and UI/UI.Web/Dockerfile.
$SdkImage = 'mcr.microsoft.com/dotnet/sdk:10.0'
$SeedAccounts = Join-Path $RepoRoot 'docker\firebase-auth-emulator\seed\auth_export\accounts.json'

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Write-Detail([string]$Text) {
    Write-Host "    $Text"
}

function Write-Caution([string]$Text) {
    Write-Host "    $Text" -ForegroundColor Yellow
}

# Runs a native command with its output shown, and throws when it fails. Error handling is relaxed for the
# call itself: docker writes progress to stderr, which some hosts (PowerShell ISE, the VS Code terminal)
# turn into error records that $ErrorActionPreference = 'Stop' would make fatal.
function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [Parameter(Mandatory = $true)][string]$Failure
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $FilePath @ArgumentList | Out-Host } finally { $ErrorActionPreference = $previous }
    if ($LASTEXITCODE -ne 0) { throw "$Failure (exit code $LASTEXITCODE)." }
}

# Runs a native command and returns its standard output, discarding standard error. The exit code is left
# in $LASTEXITCODE for the caller.
function Get-NativeOutput {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @()
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $FilePath @ArgumentList 2>$null } finally { $ErrorActionPreference = $previous }
}

# ---------------------------------------------------------------------------------------------- Docker

function Get-DockerEngineOs {
    $os = Get-NativeOutput docker @('info', '--format', '{{.OSType}}')
    if ($LASTEXITCODE -ne 0) { return $null }
    return "$os".Trim()
}

function Start-DockerEngine {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        # Docker Desktop puts its CLI on PATH, but consoles opened before the installation do not see it yet.
        $dockerBin = Join-Path $env:ProgramFiles 'Docker\Docker\resources\bin'
        if (-not (Test-Path -LiteralPath (Join-Path $dockerBin 'docker.exe'))) {
            throw 'Docker was not found. Install Docker Desktop (https://www.docker.com/products/docker-desktop/), then run this script again.'
        }
        $env:Path = "$dockerBin;$env:Path"
    }

    $os = Get-DockerEngineOs
    if (-not $os) {
        $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
        if (-not (Test-Path -LiteralPath $desktop)) {
            throw "The Docker engine is not running, and Docker Desktop is not at '$desktop'. Start Docker yourself, then run this script again."
        }
        Write-Detail 'The Docker engine is not running - starting Docker Desktop.'
        Start-Process -FilePath $desktop
        $deadline = (Get-Date).AddMinutes(5)
        Write-Host '    Waiting for the engine ' -NoNewline
        while (-not ($os = Get-DockerEngineOs)) {
            if ((Get-Date) -gt $deadline) {
                Write-Host ''
                throw 'Docker Desktop did not become ready within 5 minutes. On its very first start it waits for you to accept its terms (and may ask to install or update WSL): finish that in the Docker Desktop window, then run this script again.'
            }
            Write-Host '.' -NoNewline
            Start-Sleep -Seconds 3
        }
        Write-Host ' ready.'
    }

    if ($os -ne 'linux') {
        throw "Docker Desktop is set to Windows containers, but this stack needs Linux containers. Right-click the Docker icon in the taskbar, choose 'Switch to Linux containers...', then run this script again."
    }
    $engineVersion = Get-NativeOutput docker @('version', '--format', '{{.Server.Version}}')
    $composeVersion = Get-NativeOutput docker @('compose', 'version', '--short')
    if ($LASTEXITCODE -ne 0) {
        throw 'The Docker Compose plugin is missing. Update Docker Desktop, then run this script again.'
    }
    Write-Detail "Docker Engine $engineVersion, Compose $composeVersion."
}

# ----------------------------------------------------------------------------------------------- Ports

function Get-ComposeConfig {
    $json = Get-NativeOutput docker @('compose', 'config', '--format', 'json')
    if ($LASTEXITCODE -ne 0) {
        # Again, this time with the error output visible.
        Invoke-Native docker @('compose', 'config', '--quiet') -Failure 'compose.yaml could not be read'
        throw 'compose.yaml could not be read.'
    }
    return ($json -join "`n") | ConvertFrom-Json
}

function Get-PublishedPort($Config, [string]$Service, [int]$ContainerPort) {
    foreach ($port in @($Config.services.$Service.ports)) {
        if ($port -and [int]$port.target -eq $ContainerPort) { return [int]$port.published }
    }
    throw "compose.yaml does not publish port $ContainerPort of the '$Service' service."
}

function Assert-PortsAvailable($Config) {
    $running = @(Get-NativeOutput docker @('compose', 'ps', '--services', '--status', 'running'))
    $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue)
    $checked = 0
    $conflicts = @()
    foreach ($service in $Config.services.PSObject.Properties) {
        # A running container of this stack holds its own ports; compose up keeps or recreates it.
        if ($running -contains $service.Name) { continue }
        foreach ($port in @($service.Value.ports)) {
            if (-not $port) { continue }
            $checked++
            $listener = $listeners | Where-Object { $_.LocalPort -eq [int]$port.published } | Select-Object -First 1
            if ($listener) {
                $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
                $owner = if ($process) { "$($process.ProcessName) (PID $($listener.OwningProcess))" } else { "PID $($listener.OwningProcess)" }
                $conflicts += "Port $($port.published), needed by '$($service.Name)', is taken by $owner."
            }
        }
    }
    if ($conflicts.Count -gt 0) {
        foreach ($conflict in $conflicts) { Write-Caution $conflict }
        throw 'Free these ports, then run this script again. wslrelay or com.docker.backend means another Docker container (docker ps shows which); on 5432 it is often a locally installed PostgreSQL.'
    }
    if ($checked -eq 0) { Write-Detail 'The stack is already running.' } else { Write-Detail "All $checked ports are free." }
}

# ----------------------------------------------------------------------------------------- Certificate

# The password Kestrel will open the certificate with, as compose resolves ${DEV_CERT_PASSWORD:-changeit}: from
# the environment, a .env file, or the default. Reading the variable here directly would miss the .env file, and
# the UI would then fail to open a certificate this script had exported with a different password.
function Get-CertificatePassword($Config) {
    $password = $Config.services.ui.environment.ASPNETCORE_Kestrel__Certificates__Default__Password
    if (-not $password) {
        throw "compose.yaml no longer sets ASPNETCORE_Kestrel__Certificates__Default__Password for the 'ui' service; start.ps1 needs updating."
    }
    return $password
}

function Test-DotnetSdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { return $false }
    $sdks = @(Get-NativeOutput dotnet @('--list-sdks') | Where-Object { $_ })
    return ($LASTEXITCODE -eq 0) -and ($sdks.Count -gt 0)
}

# The certificate in certs\aspnetapp.pfx, public part only, if the file opens with the password, carries its
# private key and stays valid for at least another day; $null otherwise.
function Get-UsableCertificate {
    if (-not (Test-Path -LiteralPath $PfxPath)) { return $null }
    try {
        $pfx = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath, $CertPassword)
    } catch {
        return $null
    }
    try {
        if ($pfx.HasPrivateKey -and $pfx.NotAfter -gt (Get-Date).AddDays(1)) {
            return [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($pfx.RawData)
        }
        return $null
    } finally {
        $pfx.Dispose()   # also removes the temporary key container Windows created to open the file
    }
}

function New-Certificate {
    New-Item -ItemType Directory -Force -Path $CertDir | Out-Null
    if (Test-Path -LiteralPath $PfxPath) { Remove-Item -LiteralPath $PfxPath -Force }
    if (Test-DotnetSdk) {
        Write-Detail 'Exporting the ASP.NET Core development certificate with the installed .NET SDK.'
        Invoke-Native dotnet @('dev-certs', 'https', '--export-path', $PfxPath, '--password', $CertPassword) -Failure 'dotnet dev-certs could not export the certificate'
    } else {
        Write-Detail "No .NET SDK installed - running dotnet dev-certs inside $SdkImage."
        if (-not (Get-NativeOutput docker @('image', 'ls', '--quiet', $SdkImage))) {
            Invoke-Native docker @('pull', $SdkImage) -Failure "Pulling $SdkImage failed"
        }
        # Copied out with docker cp instead of exported into a bind-mounted certs folder: a file a container
        # writes into a Windows folder keeps its Linux owner and mode (root, 0600), and the UI container's
        # non-root user then cannot read it. A file docker cp creates is an ordinary Windows file.
        $container = "$(Get-NativeOutput docker @('create', $SdkImage, 'dotnet', 'dev-certs', 'https', '--export-path', '/tmp/aspnetapp.pfx', '--password', $CertPassword))".Trim()
        if ($LASTEXITCODE -ne 0 -or -not $container) { throw "Creating a container from $SdkImage failed." }
        try {
            Invoke-Native docker @('start', '--attach', $container) -Failure 'dotnet dev-certs failed inside the container'
            Invoke-Native docker @('cp', "${container}:/tmp/aspnetapp.pfx", $PfxPath) -Failure 'Copying the certificate out of the container failed'
        } finally {
            Get-NativeOutput docker @('rm', '--force', $container) | Out-Null
        }
    }
}

function Add-CertificateTrust($Certificate) {
    foreach ($location in 'CurrentUser', 'LocalMachine') {
        if (Test-Path -LiteralPath "Cert:\$location\Root\$($Certificate.Thumbprint)") {
            Write-Detail "Trusted by Windows ($location\Root)."
            return
        }
    }
    Write-Caution 'Adding it to your trusted root certificates. Windows asks for confirmation now - answer "Yes".'
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('Root', 'CurrentUser')
    try {
        $store.Open('ReadWrite')
        $store.Add($Certificate)
        Write-Detail 'Trusted. Restart the browser if it was already open.'
    } catch {
        Write-Caution "Not trusted: $($_.Exception.Message)"
        Write-Caution 'The UI works anyway, but the browser will show a certificate warning.'
    } finally {
        $store.Close()
    }
}

# ------------------------------------------------------------------------------------------------ Apps

# compose up --wait is not enough on its own: the Service and the UI have no health check, so compose calls
# them ready as soon as their process runs - including a UI that exits a second later and restarts forever.
function Wait-HttpResponse([string]$Name, [string]$Service, [string]$Url, [int]$TimeoutSeconds = 120) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($true) {
        # --insecure: this only asks whether the app answers. Whether the browser trusts it was step 3.
        $status = "$(Get-NativeOutput curl.exe @('--silent', '--insecure', '--max-time', '5', '--output', 'NUL', '--write-out', '%{http_code}', $Url))"
        if ($status -match '^[1-4]\d\d$') {
            Write-Detail "$Name answers: $Url -> HTTP $status."
            return
        }
        if ((Get-Date) -gt $deadline) {
            Write-Caution "$Name did not answer at $Url within $TimeoutSeconds seconds. Its last log lines:"
            $previous = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            try { & docker compose logs --no-log-prefix --tail 30 $Service 2>&1 | ForEach-Object { Write-Host "      $_" } } finally { $ErrorActionPreference = $previous }
            throw "$Name did not start (last status: $status). The log lines above usually say why; docker compose logs $Service shows all of them."
        }
        Start-Sleep -Seconds 2
    }
}

# "email / password" for every account in the emulator seed. Emulator exports keep passwords in clear
# text (fakeHash:...:password=...), see docker/firebase-auth-emulator/README.md.
function Get-SeedAccount {
    if (-not (Test-Path -LiteralPath $SeedAccounts)) { return }
    foreach ($user in (Get-Content -LiteralPath $SeedAccounts -Raw -Encoding UTF8 | ConvertFrom-Json).users) {
        if ("$($user.passwordHash)" -match ':password=(.+)$') { "$($user.email) / $($Matches[1])" }
    }
}

# ------------------------------------------------------------------------------------------------ Main

Push-Location -LiteralPath $RepoRoot
try {
    Write-Step '1/5 Docker'
    Start-DockerEngine

    Write-Step '2/5 Ports'
    $config = Get-ComposeConfig
    Assert-PortsAvailable $config

    Write-Step '3/5 HTTPS certificate'
    $CertPassword = Get-CertificatePassword $config
    $uiContainerExisted = [bool](Get-NativeOutput docker @('compose', 'ps', '--all', '--quiet', 'ui'))
    $certificate = Get-UsableCertificate
    $certificateCreated = $false
    if ($certificate) {
        Write-Detail "Using certs\aspnetapp.pfx, valid until $($certificate.NotAfter.ToString('yyyy-MM-dd'))."
    } else {
        New-Certificate
        $certificate = Get-UsableCertificate
        if (-not $certificate) { throw 'certs\aspnetapp.pfx was written, but it does not open with the password.' }
        $certificateCreated = $true
        Write-Detail "Created certs\aspnetapp.pfx, valid until $($certificate.NotAfter.ToString('yyyy-MM-dd'))."
    }
    if ($SkipCertificateTrust) {
        Write-Detail 'Leaving the Windows certificate store alone (-SkipCertificateTrust).'
    } else {
        Add-CertificateTrust $certificate
    }

    Write-Step '4/5 Containers (the first run downloads and builds the images, which takes a few minutes)'
    Invoke-Native docker @('compose', 'up', '--detach', '--build', '--wait', '--wait-timeout', '300') -Failure 'docker compose up failed; the output above says why, and docker compose logs shows the container logs'
    if ($certificateCreated -and $uiContainerExisted) {
        # Kestrel reads the certificate once at start-up, and compose up does not notice a replaced file.
        Invoke-Native docker @('compose', 'restart', 'ui') -Failure 'Restarting the UI container for the new certificate failed'
    }

    Write-Step '5/5 Apps'
    $uiUrl = "https://localhost:$(Get-PublishedPort $config 'ui' 8081)"
    $swaggerUrl = "http://localhost:$(Get-PublishedPort $config 'service' 8080)/swagger"
    $emulatorUrl = "http://localhost:$(Get-PublishedPort $config 'firebase-auth' 4000)/auth"
    $postgresPort = Get-PublishedPort $config 'postgres' 5432
    $postgres = $config.services.postgres.environment
    Wait-HttpResponse -Name 'Service' -Service 'service' -Url $swaggerUrl
    Wait-HttpResponse -Name 'UI' -Service 'ui' -Url $uiUrl

    Write-Host ''
    Write-Host 'ASMPT is running.' -ForegroundColor Green
    Write-Host ''
    Write-Host "  UI                 $uiUrl"
    foreach ($account in Get-SeedAccount) { Write-Host "                     sign in with $account (emulator seed account)" }
    Write-Host "  Service (Swagger)  $swaggerUrl"
    Write-Host "  Auth emulator UI   $emulatorUrl"
    Write-Host "  PostgreSQL         localhost:$postgresPort, database $($postgres.POSTGRES_DB), user $($postgres.POSTGRES_USER) / $($postgres.POSTGRES_PASSWORD)"
    Write-Host ''
    Write-Host '  docker compose logs -f service ui   follow the app logs (they are also written to logs\)'
    Write-Host '  docker compose stop                 stop everything; the data is kept'
    Write-Host '  docker compose down -v              remove the containers and all data'
    Write-Host '  start.cmd                           start again, e.g. after a stop or after pulling changes'
} catch {
    Write-Host ''
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}
