[CmdletBinding()]
param(
    [int]$WaitTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$exitCode = 0
$stackStarted = $false

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

    try {
        & docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
    }
    catch {
        throw 'Docker Desktop is not running or the Docker daemon is unavailable.'
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop is not running or the Docker daemon is unavailable.'
    }

    Write-Host 'Starting PostgreSQL, backend, and frontend containers...'
    $stackStarted = $true
    & docker compose up --build --wait --wait-timeout $WaitTimeoutSeconds --detach
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed to start the application stack (exit code $LASTEXITCODE)."
    }

    Invoke-HttpCheck -Uri 'http://localhost:5000/health' -Description 'Backend'
    Invoke-HttpCheck -Uri 'http://localhost:3000/' -Description 'Frontend'

    $env:SMOKE_API_URL = 'http://localhost:5000'
    $env:SMOKE_FRONTEND_URL = 'http://localhost:3000'

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
    Set-Location $repositoryRoot
    if ($stackStarted) {
        Write-Host 'Stopping test containers...'
        & docker compose down --remove-orphans
        if ($LASTEXITCODE -ne 0 -and $exitCode -eq 0) {
            $exitCode = $LASTEXITCODE
        }
    }
}

exit $exitCode
