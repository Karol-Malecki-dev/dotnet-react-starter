[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RootPath,

    [Parameter(Mandatory = $true)]
    [string]$Module,

    [Parameter(Mandatory = $true)]
    [string]$UseCase,

    [ValidateSet("Command", "Query")]
    [string]$Kind = "Query",

    [switch]$Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function ConvertTo-PascalIdentifier {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch "^[A-Za-z][A-Za-z0-9]*$") {
        throw "Value '$Value' must be a non-empty PascalCase-compatible identifier."
    }

    return $Value.Substring(0, 1).ToUpperInvariant() + $Value.Substring(1)
}

function ConvertTo-KebabCase {
    param([string]$Value)

    return [Regex]::Replace($Value, "(?<!^)([A-Z])", "-`$1").ToLowerInvariant()
}

function Write-GeneratedFile {
    param(
        [string]$Path,
        [string]$Content,
        [bool]$Overwrite
    )

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    if ((Test-Path -LiteralPath $Path) -and -not $Overwrite) {
        throw "Refusing to overwrite existing file '$Path'. Use -Force only after reviewing the generated slice."
    }

    [System.IO.File]::WriteAllText(
        $Path,
        $Content.TrimStart(),
        [System.Text.UTF8Encoding]::new($false))
}

$resolvedRoot = (Resolve-Path -LiteralPath $RootPath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
    throw "RootPath '$resolvedRoot' is not a directory."
}

if (-not (Test-Path -LiteralPath (Join-Path $resolvedRoot "backend") -PathType Container)) {
    throw "RootPath '$resolvedRoot' must contain a backend directory."
}
$moduleName = ConvertTo-PascalIdentifier $Module
$useCaseName = ConvertTo-PascalIdentifier $UseCase
$requestSuffix = if ($Kind -eq "Command") { "Command" } else { "Query" }
$requestName = "$useCaseName$requestSuffix"
$resultName = "$useCaseName`Result"
$handlerName = "$useCaseName`Handler"
$controllerName = "$useCaseName`Controller"
$moduleRoute = ConvertTo-KebabCase $moduleName
$useCaseRoute = ConvertTo-KebabCase $useCaseName
$httpAttribute = if ($Kind -eq "Command") { "HttpPost" } else { "HttpGet" }
$requestNamespace = "Application.Modules.$moduleName.$useCaseName"
$handlerNamespace = "Infrastructure.Modules.$moduleName.$useCaseName"
$controllerNamespace = "API.Modules.$moduleName.$useCaseName"
$files = @(
    @{
        Path = "backend\Application\Modules\$moduleName\$useCaseName\$requestName.cs"
        Content = @"
using MediatR;

namespace $requestNamespace;

/// <summary>
/// Represents the generated $Kind contract for the $useCaseName use case.
/// Replace the placeholder result fields and add only the input required by the use case.
/// </summary>
public sealed record $requestName : IRequest<$resultName>;

/// <summary>
/// Represents the generated result contract.
/// Replace this placeholder with the public result owned by the application contract.
/// </summary>
public sealed record $resultName(string Status);
"@
    },
    @{
        Path = "backend\Infrastructure\Modules\$moduleName\$useCaseName\$handlerName.cs"
        Content = @"
using MediatR;
using $requestNamespace;

namespace $handlerNamespace;

/// <summary>
/// Generated MediatR handler skeleton for the $useCaseName use case.
/// </summary>
public sealed class $handlerName : IRequestHandler<$requestName, $resultName>
{
    public Task<$resultName> Handle(
        $requestName request,
        CancellationToken cancellationToken)
    {
        // TODO(V8): implement the business rule, authorization, focused ports,
        // transaction ownership, and failure mapping for this real use case.
        throw new NotImplementedException(
            "Implement the generated $useCaseName handler before exposing this endpoint.");
    }
}
"@
    },
    @{
        Path = "backend\API\Modules\$moduleName\$useCaseName\$controllerName.cs"
        Content = @"
using MediatR;
using Microsoft.AspNetCore.Mvc;
using $requestNamespace;

namespace $controllerNamespace;

/// <summary>
/// Generated HTTP adapter for the $useCaseName use case.
/// </summary>
[ApiController]
[Route("api/$moduleRoute/$useCaseRoute")]
public sealed class $controllerName : ControllerBase
{
    private readonly ISender _sender;

    public $controllerName(ISender sender)
    {
        _sender = sender;
    }

    [$httpAttribute]
    public async Task<ActionResult<$resultName>> Execute(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new $requestName(), cancellationToken);
        return Ok(result);
    }
}
"@
    },
    @{
        Path = "backend\UnitTests\Modules\$moduleName\$useCaseName\${handlerName}Tests.cs"
        Content = @"
using $requestNamespace;
using $handlerNamespace;
using Xunit;

namespace UnitTests.Modules.$moduleName.$useCaseName;

public sealed class ${handlerName}Tests
{
    [Fact]
    public async Task Generated_handler_requires_business_implementation()
    {
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            new $handlerName().Handle(new $requestName(), CancellationToken.None));

        Assert.Contains("$useCaseName", exception.Message, StringComparison.Ordinal);
    }
}
"@
    },
    @{
        Path = "backend\IntegrationTests\Modules\$moduleName\$useCaseName\${controllerName}Tests.cs"
        Content = @"
using MediatR;
using $controllerNamespace;
using Xunit;

namespace IntegrationTests.Modules.$moduleName.$useCaseName;

public sealed class ${controllerName}Tests
{
    [Fact]
    public void Generated_controller_uses_the_canonical_sender_boundary()
    {
        var constructor = typeof($controllerName).GetConstructors().Single();

        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(ISender));
    }
}
"@
    }
)

$manifestPath = Join-Path $resolvedRoot ".starter\slice-manifest.json"
$manifestHistoryPath = Join-Path `
    $resolvedRoot `
    ".starter\slices\$moduleName-$useCaseName.json"

if (-not $Force.IsPresent) {
    $outputPaths = @($files | ForEach-Object { Join-Path $resolvedRoot $_.Path }) `
        + @($manifestPath, $manifestHistoryPath)
    $existingPaths = @($outputPaths | Where-Object { Test-Path -LiteralPath $_ })

    if ($existingPaths.Count -gt 0) {
        throw "Refusing to overwrite existing generated files: $($existingPaths -join ', '). Use -Force only after reviewing the generated slice."
    }
}

foreach ($file in $files) {
    $absolutePath = Join-Path $resolvedRoot $file.Path
    Write-GeneratedFile -Path $absolutePath -Content $file.Content -Overwrite $Force.IsPresent
}

$manifest = [ordered]@{
    schemaVersion = 1
    generator = "New-VsaSlice.ps1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    module = $moduleName
    useCase = $useCaseName
    kind = $Kind
    request = "$requestNamespace.$requestName"
    handler = "$handlerNamespace.$handlerName"
    controller = "$controllerNamespace.$controllerName"
    generatedFiles = @($files | ForEach-Object { $_.Path })
    implementationStatus = "skeleton"
}

$manifestContent = $manifest | ConvertTo-Json -Depth 5
Write-GeneratedFile `
    -Path $manifestPath `
    -Content $manifestContent `
    -Overwrite $Force.IsPresent

Write-GeneratedFile `
    -Path $manifestHistoryPath `
    -Content $manifestContent `
    -Overwrite $Force.IsPresent

Write-Output "Generated $Kind slice '$moduleName/$useCaseName' under '$resolvedRoot'."
Write-Output "The handler is intentionally a marked skeleton and must be implemented before production use."
