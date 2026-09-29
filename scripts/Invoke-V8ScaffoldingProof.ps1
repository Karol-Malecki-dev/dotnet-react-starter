[CmdletBinding()]
param(
    [string]$OutputDirectory = ".\artifacts\v8\generated",

    [switch]$KeepOutput
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$projectScript = Join-Path $PSScriptRoot "New-V8StarterProject.ps1"
$sliceScript = Join-Path $PSScriptRoot "New-VsaSlice.ps1"

if (Test-Path -LiteralPath $resolvedOutput) {
    if (-not $KeepOutput.IsPresent) {
        throw "Proof output '$resolvedOutput' already exists. Remove only this resolved directory or use -KeepOutput to inspect the previous result."
    }
} else {
    New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
}

$results = [System.Collections.Generic.List[object]]::new()

foreach ($variant in @("Minimal", "Full")) {
    $variantStart = [System.Diagnostics.Stopwatch]::StartNew()
    $variantPath = Join-Path $resolvedOutput $variant.ToLowerInvariant()

    & $projectScript `
        -Destination $variantPath `
        -Variant $variant `
        -Force:$KeepOutput.IsPresent
    & $sliceScript `
        -RootPath $variantPath `
        -Module "Generated" `
        -UseCase "ScaffoldedHealth" `
        -Kind "Query" `
        -Force:$KeepOutput.IsPresent
    & $sliceScript `
        -RootPath $variantPath `
        -Module "Generated" `
        -UseCase "CreateScaffoldedHealth" `
        -Kind "Command" `
        -Force:$KeepOutput.IsPresent

    $manifestPath = Join-Path $variantPath ".starter\manifest.json"
    $sliceManifestPath = Join-Path $variantPath ".starter\slice-manifest.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $sliceManifest = Get-Content -LiteralPath $sliceManifestPath -Raw | ConvertFrom-Json
    if (($manifest.variant -ne $variant.ToLowerInvariant()) `
        -or ($manifest.templateVersion -ne "8.0.0") `
        -or ($sliceManifest.kind -ne "Command") `
        -or ($sliceManifest.implementationStatus -ne "skeleton")) {
        throw "Generated manifest validation failed for $variant variant."
    }

    if (-not ($manifest.modules -is [System.Array])) {
        throw "Generated manifest modules must be a JSON array for $variant variant."
    }

    foreach ($sliceManifestFile in @(
        ".starter\slices\Generated-ScaffoldedHealth.json",
        ".starter\slices\Generated-CreateScaffoldedHealth.json")) {
        if (-not (Test-Path -LiteralPath (Join-Path $variantPath $sliceManifestFile))) {
            throw "Expected slice manifest '$sliceManifestFile' was not generated for $variant variant."
        }
    }

    if (Test-Path -LiteralPath (Join-Path $variantPath "Variants")) {
        throw "Generated $variant variant leaked source template variants into its output."
    }

    $instructionPaths = @(
        ".github\copilot-instructions.md",
        ".github\instructions\backend.instructions.md"
    )
    foreach ($instructionPath in $instructionPaths) {
        $sourceInstructionPath = Join-Path $repositoryRoot $instructionPath
        $generatedInstructionPath = Join-Path $variantPath $instructionPath

        if (-not (Test-Path -LiteralPath $generatedInstructionPath -PathType Leaf)) {
            throw "Generated $variant variant is missing '$instructionPath'."
        }

        $sourceInstruction = [System.IO.File]::ReadAllText($sourceInstructionPath)
        $generatedInstruction = [System.IO.File]::ReadAllText($generatedInstructionPath)
        if ($sourceInstruction -cne $generatedInstruction) {
            throw "Generated $variant variant instruction '$instructionPath' differs from the repository baseline."
        }
    }

    $instructionFiles = @(
        (Join-Path $repositoryRoot ".github\copilot-instructions.md"),
        (Join-Path $repositoryRoot ".github\instructions\backend.instructions.md"),
        (Join-Path $variantPath ".github\copilot-instructions.md"),
        (Join-Path $variantPath ".github\instructions\backend.instructions.md")
    )
    $privateInstructionPatterns = @(
        "GPT-\d",
        "(?i)user profile",
        "(?i)model preference",
        "(?i)learning history",
        "(?i)profil użytkownika",
        "(?i)preferencje modelu",
        "(?i)użytkownik uczy"
    )
    foreach ($instructionFile in $instructionFiles) {
        $instructionContent = [System.IO.File]::ReadAllText($instructionFile)
        foreach ($privateInstructionPattern in $privateInstructionPatterns) {
            if ($instructionContent -match $privateInstructionPattern) {
                throw "Instruction file '$instructionFile' contains a private or user-specific pattern."
            }
        }
    }

    $backendInstructionContent = [System.IO.File]::ReadAllText(
        (Join-Path $variantPath ".github\instructions\backend.instructions.md"))
    if ($backendInstructionContent -notmatch '(?m)^applyTo:\s*"backend/\*\*/\*"\s*$') {
        throw "Generated $variant variant backend instruction has an invalid applyTo value."
    }

    Push-Location (Join-Path $variantPath "backend")
    try {
        dotnet restore backend.slnx
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed for $variant variant."
        }

        dotnet build backend.slnx --configuration Release --no-restore -warnaserror
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for $variant variant."
        }

        dotnet test UnitTests\UnitTests.csproj --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) {
            throw "UnitTests failed for $variant variant."
        }

        dotnet test IntegrationTests\IntegrationTests.csproj --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) {
            throw "IntegrationTests failed for $variant variant."
        }

        Push-Location (Join-Path $variantPath "frontend")
        try {
            npm install --no-audit --no-fund --ignore-scripts --package-lock=false
            if ($LASTEXITCODE -ne 0) {
                throw "npm install failed for $variant variant."
            }

            npm run build
            if ($LASTEXITCODE -ne 0) {
                throw "Frontend build failed for $variant variant."
            }
        }
        finally {
            Pop-Location
        }
    }
    finally {
        Pop-Location
    }

    $variantStart.Stop()
    $generatedFiles = @(Get-ChildItem -LiteralPath $variantPath -File -Recurse |
        Where-Object {
            $_.FullName -notmatch "[\\/](bin|obj|TestResults|node_modules|dist)([\\/]|$)"
        })

    $results.Add([ordered]@{
        variant = $variant.ToLowerInvariant()
        elapsedMilliseconds = $variantStart.ElapsedMilliseconds
        fileCount = $generatedFiles.Count
        sliceManifest = ".starter\slice-manifest.json"
        status = "passed"
    })
}

$evidence = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    repositoryRoot = $repositoryRoot
    generator = "New-V8StarterProject.ps1 + New-VsaSlice.ps1"
    variants = $results
    validation = @(
        "dotnet restore",
        "dotnet build --configuration Release --no-restore -warnaserror",
        "dotnet test UnitTests",
        "dotnet test IntegrationTests",
        "npm install --no-audit --no-fund --ignore-scripts --package-lock=false",
        "npm run build"
    )
}

$evidencePath = Join-Path $resolvedOutput "proof.json"
[System.IO.File]::WriteAllText(
    $evidencePath,
    ($evidence | ConvertTo-Json -Depth 8),
    [System.Text.UTF8Encoding]::new($false))

Write-Output "V8 scaffolding proof passed for minimal and full variants."
Write-Output "Evidence: $evidencePath"
