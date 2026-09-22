[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [uri]$BaseUrl,

    [Parameter(Mandatory = $false)]
    [string]$AccessToken = $env:V6_BASELINE_ACCESS_TOKEN,

    [Parameter(Mandatory = $true)]
    [Guid]$ProjectId,

    [ValidateRange(0, 1000)]
    [int]$WarmupRequests = 5,

    [ValidateRange(1, 10000)]
    [int]$MeasuredRequests = 50,

    [ValidateRange(1, 300)]
    [int]$TimeoutSeconds = 30,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

function Format-Decimal {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return 'n/a'
    }

    return ([double]$Value).ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
}

function Get-Percentile {
    param(
        [Parameter(Mandatory = $true)]
        [double[]]$Values,

        [Parameter(Mandatory = $true)]
        [ValidateRange(0, 100)]
        [double]$Percentile
    )

    if ($Values.Count -eq 0) {
        return $null
    }

    $orderedValues = @($Values | Sort-Object)
    $rank = [math]::Ceiling(($Percentile / 100) * $orderedValues.Count)
    $index = [math]::Max(0, [int]$rank - 1)
    return [double]$orderedValues[$index]
}

function Invoke-MeasuredRequest {
    param(
        [Parameter(Mandatory = $true)]
        [System.Net.Http.HttpClient]$Client,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Get,
        $Path)
    $response = $null
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $response = $Client.SendAsync(
            $request,
            [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $stopwatch.Stop()

        return [pscustomobject]@{
            Success     = $response.IsSuccessStatusCode
            StatusCode  = [int]$response.StatusCode
            LatencyMs   = $stopwatch.Elapsed.TotalMilliseconds
            PayloadBytes = $body.Length
            Error       = $null
        }
    }
    catch {
        $stopwatch.Stop()

        return [pscustomobject]@{
            Success      = $false
            StatusCode   = 0
            LatencyMs    = $stopwatch.Elapsed.TotalMilliseconds
            PayloadBytes = 0
            Error        = $_.Exception.Message
        }
    }
    finally {
        if ($null -ne $response) {
            $response.Dispose()
        }

        $request.Dispose()
    }
}

if ($BaseUrl.Scheme -notin @('http', 'https')) {
    throw "BaseUrl must use http or https: $BaseUrl"
}

if ([string]::IsNullOrWhiteSpace($AccessToken)) {
    throw 'AccessToken is required. Prefer setting V6_BASELINE_ACCESS_TOKEN instead of passing it in shell history.'
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssZ')
    $OutputPath = Join-Path $PSScriptRoot "..\artifacts\v6\baseline-$timestamp.md"
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$baseUri = $BaseUrl.AbsoluteUri.TrimEnd('/')
$projectPath = "/api/projects/$($ProjectId.ToString())"
$scenarios = @(
    [pscustomobject]@{
        Name = 'projects.list'
        Path = '/api/projects?scope=all'
        Purpose = 'Visible project projection and membership visibility query'
    },
    [pscustomobject]@{
        Name = 'project.tasks.page-1'
        Path = "$projectPath/tasks?pageNumber=1&pageSize=20&sortBy=createdAt&sortDirection=descending"
        Purpose = 'Count plus ordered, paged task query with label projection'
    },
    [pscustomobject]@{
        Name = 'project.dashboard'
        Path = "$projectPath/dashboard"
        Purpose = 'Composed task metrics, due dates, and recent activity read'
    }
)

$handler = [System.Net.Http.HttpClientHandler]::new()
$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [uri]$baseUri
$client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
$client.DefaultRequestHeaders.Authorization =
    [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AccessToken)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('dotnet-react-starter-v6-baseline/1.0')

$scenarioReports = [System.Collections.Generic.List[object]]::new()
$hasFailures = $false

try {
    foreach ($scenario in $scenarios) {
        Write-Host "Warming up $($scenario.Name) with $WarmupRequests request(s)..."

        for ($warmupIndex = 0; $warmupIndex -lt $WarmupRequests; $warmupIndex++) {
            $warmupResult = Invoke-MeasuredRequest -Client $client -Path $scenario.Path
            if (-not $warmupResult.Success) {
                throw "Warmup failed for $($scenario.Name) with HTTP $($warmupResult.StatusCode): $($warmupResult.Error)"
            }
        }

        Write-Host "Measuring $($scenario.Name) with $MeasuredRequests request(s)..."
        $measurements = [System.Collections.Generic.List[object]]::new()
        $wallClock = [System.Diagnostics.Stopwatch]::StartNew()

        for ($requestIndex = 0; $requestIndex -lt $MeasuredRequests; $requestIndex++) {
            $measurements.Add(
                (Invoke-MeasuredRequest -Client $client -Path $scenario.Path))
        }

        $wallClock.Stop()
        $successfulMeasurements = @($measurements | Where-Object { $_.Success })
        $failedMeasurements = @($measurements | Where-Object { -not $_.Success })
        $latencies = @($successfulMeasurements | ForEach-Object { [double]$_.LatencyMs })
        $payloadSizes = @($successfulMeasurements | ForEach-Object { [double]$_.PayloadBytes })
        $statusCounts = @(
            $measurements |
                Group-Object -Property StatusCode |
                Sort-Object { [int]$_.Name } |
                ForEach-Object { "$($_.Name): $($_.Count)" }
        )
        $averageLatency = if ($latencies.Count -gt 0) {
            ($latencies | Measure-Object -Average).Average
        }
        else {
            $null
        }
        $averagePayload = if ($payloadSizes.Count -gt 0) {
            ($payloadSizes | Measure-Object -Average).Average
        }
        else {
            $null
        }
        $elapsedSeconds = $wallClock.Elapsed.TotalSeconds
        $throughput = if ($elapsedSeconds -gt 0) {
            $measurements.Count / $elapsedSeconds
        }
        else {
            $null
        }

        $scenarioReport = [pscustomobject]@{
            Name             = $scenario.Name
            Path             = $scenario.Path
            Purpose          = $scenario.Purpose
            Requested        = $measurements.Count
            Successful       = $successfulMeasurements.Count
            Failed           = $failedMeasurements.Count
            ErrorRate        = if ($measurements.Count -gt 0) {
                $failedMeasurements.Count / $measurements.Count * 100
            }
            else {
                0
            }
            P50              = Get-Percentile -Values $latencies -Percentile 50
            P95              = Get-Percentile -Values $latencies -Percentile 95
            P99              = Get-Percentile -Values $latencies -Percentile 99
            AverageLatency   = $averageLatency
            AveragePayload   = $averagePayload
            Throughput       = $throughput
            ElapsedSeconds   = $elapsedSeconds
            StatusCounts     = $statusCounts
            Errors           = @($failedMeasurements | ForEach-Object { $_.Error } | Where-Object { $_ })
        }

        $scenarioReports.Add($scenarioReport)
        if ($scenarioReport.Failed -gt 0) {
            $hasFailures = $true
        }
    }

    $report = [System.Collections.Generic.List[string]]::new()
    $report.Add('# V6 API baseline')
    $report.Add('')
    $report.Add("- Generated UTC: $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))")
    $report.Add("- Base URL: $baseUri")
    $report.Add("- Project ID: $ProjectId")
    $report.Add("- Warmup requests per scenario: $WarmupRequests")
    $report.Add("- Measured requests per scenario: $MeasuredRequests")
    $report.Add("- Concurrency: 1 (sequential closed-loop measurement)")
    $report.Add("- Timeout per request: $TimeoutSeconds seconds")
    $report.Add('')
    $report.Add('> This report is a repeatable application baseline, not a capacity or stress test. Run it against a PostgreSQL-backed environment with a documented fixture and keep the environment parameters with the report.')
    $report.Add('')
    $report.Add('## Scenarios')
    $report.Add('')
    $report.Add('| Scenario | Endpoint | Purpose |')
    $report.Add('|---|---|---|')

    foreach ($scenario in $scenarioReports) {
        $report.Add("| $($scenario.Name) | ``$($scenario.Path)`` | $($scenario.Purpose) |")
    }

    $report.Add('')
    $report.Add('## Results')
    $report.Add('')
    $report.Add('| Scenario | Requests | Success | Errors | Error rate | p50 ms | p95 ms | p99 ms | Avg ms | Avg payload bytes | Throughput req/s |')
    $report.Add('|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|')

    foreach ($scenario in $scenarioReports) {
        $report.Add(
            "| $($scenario.Name) | $($scenario.Requested) | $($scenario.Successful) | $($scenario.Failed) | $(Format-Decimal $scenario.ErrorRate)% | $(Format-Decimal $scenario.P50) | $(Format-Decimal $scenario.P95) | $(Format-Decimal $scenario.P99) | $(Format-Decimal $scenario.AverageLatency) | $(Format-Decimal $scenario.AveragePayload) | $(Format-Decimal $scenario.Throughput) |")
    }

    $report.Add('')
    $report.Add('## HTTP status distribution')
    $report.Add('')

    foreach ($scenario in $scenarioReports) {
        $report.Add("- **$($scenario.Name):** $($scenario.StatusCounts -join ', ')")
    }

    $report.Add('')
    $report.Add('## Interpretation notes')
    $report.Add('')
    $report.Add('- Latency percentiles use the nearest-rank value over successful measured requests only.')
    $report.Add('- Error rate includes non-2xx responses and transport exceptions.')
    $report.Add('- Throughput is measured requests divided by wall-clock duration for the sequential run.')
    $report.Add('- Compare reports only when the application version, database engine, fixture size, request parameters, and host resources are recorded.')
    $report.Add('- Capture PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` plans separately before changing indexes or projections.')

    $report | Set-Content -Path $OutputPath -Encoding utf8
    Write-Host "Baseline report written to $OutputPath"
}
finally {
    $client.Dispose()
    $handler.Dispose()
}

if ($hasFailures) {
    exit 1
}
