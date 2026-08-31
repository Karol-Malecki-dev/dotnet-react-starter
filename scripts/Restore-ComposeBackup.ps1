[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [string]$BackupDirectory,

    [switch]$Force,

    [string]$DatabaseUser = $(if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'postgres' }),
    [string]$DatabaseName = $(if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { 'dotnetreact' })
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source = (Resolve-Path $BackupDirectory).Path
$manifestPath = Join-Path $source 'manifest.json'
$databaseDumpPath = Join-Path $source 'database.dump'
$attachmentSource = Join-Path $source 'attachments'
$databaseContainerPath = "/tmp/dotnet-react-restore-$([Guid]::NewGuid().ToString('N')).dump"

function Invoke-DockerCompose {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & docker compose @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not $Force.IsPresent) {
    throw 'Restore replaces the current database and attachments. Re-run with -Force after verifying the backup target.'
}

$requiredPaths = @($manifestPath, $databaseDumpPath, $attachmentSource)
foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path $requiredPath)) {
        throw 'The backup must contain manifest.json, database.dump, and the attachments directory.'
    }
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
foreach ($file in $manifest.Files) {
    $filePath = Join-Path $source $file.Path
    if (-not (Test-Path $filePath -PathType Leaf)) {
        throw "Backup file is missing: $($file.Path)"
    }
    $actualHash = (Get-FileHash -Path $filePath -Algorithm SHA256).Hash
    if ($actualHash -ne $file.Sha256) {
        throw "Backup checksum mismatch: $($file.Path)"
    }
}

if (-not $PSCmdlet.ShouldProcess($repositoryRoot, "Restore database '$DatabaseName' and attachment binaries")) {
    return
}

Push-Location $repositoryRoot
try {
    Invoke-DockerCompose @('config', '--quiet')
    $runningServices = @(& docker compose ps --status running --services)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to determine running Compose services.'
    }

    Invoke-DockerCompose @('stop', 'frontend', 'backend')
    Invoke-DockerCompose @('cp', $databaseDumpPath, "db:$databaseContainerPath")
    Invoke-DockerCompose @(
        'exec', '-T', 'db',
        'pg_restore', '-U', $DatabaseUser, '-d', $DatabaseName,
        '--clean', '--if-exists', '--no-owner', '--no-privileges',
        $databaseContainerPath
    )
    Invoke-DockerCompose @('exec', '-T', 'db', 'rm', '-f', $databaseContainerPath)
    Invoke-DockerCompose @(
        'run', '--rm', '--no-deps', '--entrypoint', 'sh', 'backend',
        '-c', 'find /app/data/task-attachments -mindepth 1 -maxdepth 1 -exec rm -rf {} +'
    )
    Invoke-DockerCompose @('cp', "$attachmentSource/.", 'backend:/app/data/task-attachments')
    Write-Output "Restore completed from $source"
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
