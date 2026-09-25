[CmdletBinding()]
param(
    [int]$WaitTimeoutSeconds = 180,
    [string]$ComposeProjectName = '',
    [int]$PostgresPort = 0,
    [int]$MailpitSmtpPort = 0,
    [int]$MailpitHttpPort = 0,
    [int]$MinioApiPort = 0,
    [int]$MinioConsolePort = 0,
    [int]$BackendHttpPort = 0,
    [int]$FrontendHttpPort = 0
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$exitCode = 0
$stackStarted = $false
$environmentVariableNames = @(
    'COMPOSE_PROJECT_NAME',
    'POSTGRES_PORT',
    'MAILPIT_SMTP_PORT',
    'MAILPIT_HTTP_PORT',
    'MINIO_API_PORT',
    'MINIO_CONSOLE_PORT',
    'BACKEND_HTTP_PORT',
    'FRONTEND_HTTP_PORT',
    'CORS_ALLOWED_ORIGIN_0',
    'EMAIL_CONFIRMATION_PUBLIC_ORIGIN',
    'SMOKE_API_URL',
    'SMOKE_FRONTEND_URL',
    'E2E_FRONTEND_URL',
    'E2E_MAILPIT_URL'
)
$originalEnvironment = @{}
foreach ($name in $environmentVariableNames) {
    $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

if ([string]::IsNullOrWhiteSpace($ComposeProjectName)) {
    $ComposeProjectName = "dotnet-react-e2e-$PID"
}

if ($ComposeProjectName -notmatch '^[a-z0-9][a-z0-9_-]*$') {
    throw "Compose project name '$ComposeProjectName' is invalid. Use lowercase letters, numbers, hyphens, or underscores."
}

$reservedPorts = [System.Collections.Generic.HashSet[int]]::new()

function Get-FreeTcpPort {
    param(
        [int]$PreferredPort,
        [System.Collections.Generic.HashSet[int]]$ReservedPorts
    )

    for ($port = $PreferredPort; $port -le $PreferredPort + 100; $port++) {
        if ($ReservedPorts.Contains($port)) {
            continue
        }

        $listener = [System.Net.Sockets.TcpListener]::new(
            [System.Net.IPAddress]::Loopback,
            $port)
        try {
            $listener.Start()
            $ReservedPorts.Add($port) | Out-Null
            return $port
        }
        catch [System.Net.Sockets.SocketException] {
            continue
        }
        finally {
            $listener.Stop()
        }
    }

    throw "Could not find a free TCP port near $PreferredPort."
}

function Resolve-HostPort {
    param(
        [int]$ConfiguredPort,
        [int]$PreferredPort,
        [System.Collections.Generic.HashSet[int]]$ReservedPorts
    )

    if ($ConfiguredPort -gt 0) {
        if (-not $ReservedPorts.Add($ConfiguredPort)) {
            throw "Configured host port $ConfiguredPort is assigned to more than one service."
        }

        return $ConfiguredPort
    }

    return Get-FreeTcpPort -PreferredPort $PreferredPort -ReservedPorts $ReservedPorts
}

function Assert-CommandAvailable {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found. Install Docker Desktop and ensure Docker Compose is available."
    }
}

function Invoke-HttpCheck {
    param(
        [string]$Uri,
        [string]$Description
    )

    try {
        $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 400) {
            throw "HTTP status $($response.StatusCode)"
        }

        Write-Host "$Description is ready: $Uri"
    }
    catch {
        throw "$Description is not ready at $Uri. $($_.Exception.Message)"
    }
}

try {
    Assert-CommandAvailable -Name 'docker'
    Assert-CommandAvailable -Name 'dotnet'
    Assert-CommandAvailable -Name 'npm'

    Set-Location $repositoryRoot

    $PostgresPort = Resolve-HostPort -ConfiguredPort $PostgresPort -PreferredPort 5432 -ReservedPorts $reservedPorts
    $MailpitSmtpPort = Resolve-HostPort -ConfiguredPort $MailpitSmtpPort -PreferredPort 1025 -ReservedPorts $reservedPorts
    $MailpitHttpPort = Resolve-HostPort -ConfiguredPort $MailpitHttpPort -PreferredPort 8025 -ReservedPorts $reservedPorts
    $MinioApiPort = Resolve-HostPort -ConfiguredPort $MinioApiPort -PreferredPort 9000 -ReservedPorts $reservedPorts
    $MinioConsolePort = Resolve-HostPort -ConfiguredPort $MinioConsolePort -PreferredPort 9001 -ReservedPorts $reservedPorts
    $BackendHttpPort = Resolve-HostPort -ConfiguredPort $BackendHttpPort -PreferredPort 5000 -ReservedPorts $reservedPorts
    $FrontendHttpPort = Resolve-HostPort -ConfiguredPort $FrontendHttpPort -PreferredPort 3000 -ReservedPorts $reservedPorts

    $env:COMPOSE_PROJECT_NAME = $ComposeProjectName
    $env:POSTGRES_PORT = $PostgresPort
    $env:MAILPIT_SMTP_PORT = $MailpitSmtpPort
    $env:MAILPIT_HTTP_PORT = $MailpitHttpPort
    $env:MINIO_API_PORT = $MinioApiPort
    $env:MINIO_CONSOLE_PORT = $MinioConsolePort
    $env:BACKEND_HTTP_PORT = $BackendHttpPort
    $env:FRONTEND_HTTP_PORT = $FrontendHttpPort
    $env:CORS_ALLOWED_ORIGIN_0 = "http://localhost:$FrontendHttpPort"
    $env:EMAIL_CONFIRMATION_PUBLIC_ORIGIN = "http://localhost:$FrontendHttpPort"
    $env:SMOKE_API_URL = "http://localhost:$BackendHttpPort"
    $env:SMOKE_FRONTEND_URL = "http://localhost:$FrontendHttpPort"
    $env:E2E_FRONTEND_URL = "http://localhost:$FrontendHttpPort"
    $env:E2E_MAILPIT_URL = "http://localhost:$MailpitHttpPort"

    try {
        & docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
    }
    catch {
        throw 'Docker Desktop is not running or the Docker daemon is unavailable.'
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop is not running or the Docker daemon is unavailable.'
    }

    Write-Host "Starting Compose project '$ComposeProjectName'..."
    Write-Host "Frontend: http://localhost:$FrontendHttpPort; Backend: http://localhost:$BackendHttpPort; Mailpit: http://localhost:$MailpitHttpPort"
    $stackStarted = $true
    & docker compose --project-name $ComposeProjectName up --build --wait --wait-timeout $WaitTimeoutSeconds --detach
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed to start the application stack (exit code $LASTEXITCODE)."
    }

    Invoke-HttpCheck -Uri "http://localhost:$BackendHttpPort/health" -Description 'Backend'
    Invoke-HttpCheck -Uri "http://localhost:$FrontendHttpPort/" -Description 'Frontend'

    Write-Host 'Building and testing the backend release...'
    & dotnet build backend/backend.slnx --configuration Release
    if ($LASTEXITCODE -ne 0) {
        $exitCode = $LASTEXITCODE
    }

    & dotnet test backend/backend.slnx --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) {
        $exitCode = $LASTEXITCODE
    }

    Write-Host 'Building and testing the frontend release...'
    Push-Location (Join-Path $repositoryRoot 'frontend')
    try {
        & npm ci
        if ($LASTEXITCODE -ne 0) { $exitCode = $LASTEXITCODE }
        & npm run test:once
        if ($LASTEXITCODE -ne 0) { $exitCode = $LASTEXITCODE }
        & npm run build
        if ($LASTEXITCODE -ne 0) { $exitCode = $LASTEXITCODE }
        & npm run test:e2e
        if ($LASTEXITCODE -ne 0) { $exitCode = $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Error $_
    $exitCode = 1
}
finally {
    try {
        Set-Location $repositoryRoot
        if ($stackStarted) {
            Write-Host 'Stopping test containers...'
            & docker compose --project-name $ComposeProjectName down --remove-orphans
            if ($LASTEXITCODE -ne 0 -and $exitCode -eq 0) {
                $exitCode = $LASTEXITCODE
            }
        }
    }
    finally {
        foreach ($name in $environmentVariableNames) {
            $originalValue = $originalEnvironment[$name]
            if ($null -eq $originalValue) {
                Remove-Item "Env:$name" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "Env:$name" $originalValue
            }
        }
    }
}

exit $exitCode
