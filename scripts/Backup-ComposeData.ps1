[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "../artifacts/backups/$(Get-Date -Format 'yyyyMMdd-HHmmss')"),
    [string]$DatabaseUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'postgres' }),
    [string]$DatabaseName = $(if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { 'dotnetreact' })
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$destination = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
$databaseContainerPath = "/tmp/dotnet-react-backup-$([Guid]::NewGuid().ToString('N')).dump"

function Invoke-DockerCompose {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & docker compose @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    Invoke-DockerCompose @('config', '--quiet')
    $runningServices = @(& docker compose ps --status running --services)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to determine running Compose services.'
    }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $attachmentDestination = Join-Path $destination 'attachments'
    New-Item -ItemType Directory -Path $attachmentDestination -Force | Out-Null

    Invoke-DockerCompose @('stop', 'frontend', 'backend')
    Invoke-DockerCompose @(
        'exec', '-T', 'db',
        'pg_dump', '-U', $DatabaseUser, '-d', $DatabaseName,
        '--format=custom', '--no-owner', '--no-privileges',
        "--file=$databaseContainerPath"
    )
    Invoke-DockerCompose @('cp', "db:$databaseContainerPath", (Join-Path $destination 'database.dump'))
    Invoke-DockerCompose @('exec', '-T', 'db', 'rm', '-f', $databaseContainerPath)
    Invoke-DockerCompose @('cp', 'backend:/app/data/task-attachments/.', $attachmentDestination)

    $files = @(Get-ChildItem -Path $destination -File -Recurse | ForEach-Object {
        [ordered]@{
            Path = [System.IO.Path]::GetRelativePath($destination, $_.FullName).Replace('\', '/')
            SizeBytes = $_.Length
            Sha256 = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
        }
    })
    $manifest = [ordered]@{
        CreatedAtUtc = [DateTime]::UtcNow.ToString('O')
        DatabaseName = $DatabaseName
        ComposeProject = Split-Path $repositoryRoot -Leaf
        Files = $files
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $destination 'manifest.json') -Encoding utf8
    Write-Output "Backup created at $destination"
}
finally {
    if ($runningServices -contains 'backend') {
        Invoke-DockerCompose @('start', 'backend')
    }
    if ($runningServices -contains 'frontend') {
        Invoke-DockerCompose @('start', 'frontend')
    }
    Pop-Location
}
