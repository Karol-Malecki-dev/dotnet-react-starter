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
$dataProtectionSource = Join-Path $source 'data-protection-keys'
$databaseContainerPath = "/tmp/dotnet-react-restore-$([Guid]::NewGuid().ToString('N')).dump"

function Invoke-DockerCompose {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & docker compose @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not $Force.IsPresent) {
    throw 'Restore replaces the current database, attachment objects, and Data Protection keys. Re-run with -Force after verifying the backup target.'
}

$requiredPaths = @($manifestPath, $databaseDumpPath, $attachmentSource, $dataProtectionSource)
foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path $requiredPath)) {
        throw 'The backup must contain manifest.json, database.dump, attachments, and data-protection-keys.'
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

if (-not $PSCmdlet.ShouldProcess($repositoryRoot, "Restore database '$DatabaseName', attachment objects, and Data Protection keys")) {
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
        '-c', 'find /home/app/.aspnet/DataProtection-Keys -mindepth 1 -maxdepth 1 -exec rm -rf {} +'
    )
    Invoke-DockerCompose @('cp', "$dataProtectionSource/.", 'backend:/home/app/.aspnet/DataProtection-Keys')
    Invoke-DockerCompose @(
        'run', '--rm', '--no-deps', '--volume', "${attachmentSource}:/backup:ro",
        '--entrypoint', '/bin/sh', 'minio-init', '-c',
        'mc alias set local http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" && mc rm --recursive --force "local/$MINIO_BUCKET" && mc mirror /backup "local/$MINIO_BUCKET"'
    )
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
