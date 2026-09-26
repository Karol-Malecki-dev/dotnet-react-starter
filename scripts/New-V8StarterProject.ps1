[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination,

    [ValidateSet("Minimal", "Full")]
    [string]$Variant = "Minimal",

    [switch]$Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-TemplateFile {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [bool]$Overwrite
    )

    $directory = Split-Path -Parent $DestinationPath
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    if ((Test-Path -LiteralPath $DestinationPath) -and -not $Overwrite) {
        throw "Refusing to overwrite existing file '$DestinationPath'. Use -Force only for a disposable generated project."
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force:$Overwrite
}

function Copy-TemplateTree {
    param(
        [string]$SourceRoot,
        [string]$DestinationRoot,
        [bool]$Overwrite
    )

    $resolvedSourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd("\", "/")

    foreach ($sourceFile in Get-ChildItem -LiteralPath $resolvedSourceRoot -File -Recurse |
        Where-Object {
            $_.FullName -notmatch "[\\/](bin|obj|TestResults|node_modules|\.git)([\\/]|$)"
        }) {
        $relativePath = $sourceFile.FullName.Substring($resolvedSourceRoot.Length).TrimStart("\", "/")
        $destinationFile = Join-Path $DestinationRoot $relativePath
        Write-TemplateFile `
            -SourcePath $sourceFile.FullName `
            -DestinationPath $destinationFile `
            -Overwrite $Overwrite
    }
}

$scriptRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$templateRoot = Join-Path $scriptRoot "..\templates\v8-consumer"
$commonRoot = Join-Path $templateRoot "Common"
$resolvedDestination = [System.IO.Path]::GetFullPath($Destination)

if (-not (Test-Path -LiteralPath $commonRoot -PathType Container)) {
    throw "Common V8 template directory '$commonRoot' was not found."
}

if ((Test-Path -LiteralPath $resolvedDestination) -and -not $Force.IsPresent) {
    if (-not (Test-Path -LiteralPath $resolvedDestination -PathType Container)) {
        throw "Destination '$resolvedDestination' exists and is not a directory."
    }

    $existingEntries = @(Get-ChildItem -LiteralPath $resolvedDestination -Force)
    if ($existingEntries.Count -gt 0) {
        throw "Destination '$resolvedDestination' is not empty. Use a new directory or -Force for a disposable generated project."
    }
}

New-Item -ItemType Directory -Path $resolvedDestination -Force | Out-Null
Copy-TemplateTree `
    -SourceRoot $commonRoot `
    -DestinationRoot $resolvedDestination `
    -Overwrite $Force.IsPresent

if ($Variant -eq "Full") {
    $fullRoot = Join-Path $templateRoot "Variants\Full"
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        throw "Full V8 template directory '$fullRoot' was not found."
    }

    Copy-TemplateTree `
        -SourceRoot $fullRoot `
        -DestinationRoot $resolvedDestination `
        -Overwrite $Force.IsPresent
}

$modules = if ($Variant -eq "Full") {
    @("StarterHealth", "Catalog")
} else {
    @("StarterHealth")
}

$manifest = [ordered]@{
    schemaVersion = 1
    templateVersion = "8.0.0"
    variant = $Variant.ToLowerInvariant()
    distribution = "source-template"
    modules = @($modules)
    updatePolicy = "regenerate-and-review"
    databasePolicy = "single-postgresql-context"
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
}

$starterDirectory = Join-Path $resolvedDestination ".starter"
New-Item -ItemType Directory -Path $starterDirectory -Force | Out-Null
[System.IO.File]::WriteAllText(
    (Join-Path $starterDirectory "manifest.json"),
    ($manifest | ConvertTo-Json -Depth 5),
    [System.Text.UTF8Encoding]::new($false))

$readmePath = Join-Path $resolvedDestination "V8_GENERATED_PROJECT.md"
$readme = @"
# Generated V8 starter project

- Variant: $($Variant.ToLowerInvariant())
- Template version: 8.0.0
- Modules: $($modules -join ", ")

This project was generated from the repository's bounded V8 consumer template.
The template demonstrates the supported project composition and MediatR slice
boundary. It does not invent domain rules, persistence models, authorization
policies, or migrations.

Run the backend checks from `backend`:

```powershell
dotnet build backend.slnx --configuration Release
dotnet test UnitTests\UnitTests.csproj --configuration Release
dotnet test IntegrationTests\IntegrationTests.csproj --configuration Release
```

Use `scripts\New-VsaSlice.ps1` from the source repository to add a new slice,
then replace its explicitly marked skeleton before exposing it to users.
"@

[System.IO.File]::WriteAllText(
    $readmePath,
    $readme,
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Generated V8 $($Variant.ToLowerInvariant()) starter project at '$resolvedDestination'."
