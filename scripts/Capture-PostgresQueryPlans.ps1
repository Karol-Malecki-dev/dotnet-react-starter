[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Guid]$ProjectId,

    [Guid]$UserId,

    [string]$ComposeFile = 'docker-compose.yml',

    [string]$EnvFile,

    [string]$DbService = 'db',

    [string]$DbUser = 'postgres',

    [string]$DbName = 'dotnetreact',

    [string]$ApplicationVersion = $env:V6_APPLICATION_VERSION,

    [string]$FixtureDescription = $env:V6_FIXTURE_DESCRIPTION,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Invoke-ComposePsql {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $composeArguments = @('compose')
    if (-not [string]::IsNullOrWhiteSpace($EnvFile)) {
        $composeArguments += @('--env-file', $EnvFile)
    }

    $composeArguments += @(
        '-f',
        $ComposeFile,
        'exec',
        '-T',
        $DbService,
        'psql',
        '-v',
        'ON_ERROR_STOP=1',
        '-P',
        'pager=off',
        '-U',
        $DbUser,
        '-d',
        $DbName,
        '-c',
        $Sql
    )

    $lines = @(& docker @composeArguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw 'PostgreSQL command failed.'
    }

    foreach ($line in $lines) {
        [string]$line
    }
}

function Add-ExplainPlan {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IList]$Report,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $Report.Add("## $Name")
    $Report.Add('')
    $Report.Add('### SQL')
    $Report.Add('')
    $Report.Add('```sql')
    foreach ($line in ($Sql.Trim() -split "`r?`n")) {
        $Report.Add($line)
    }
    $Report.Add('```')
    $Report.Add('')
    $Report.Add('### Plan')
    $Report.Add('')
    $Report.Add('```text')

    try {
        $planLines = @(Invoke-ComposePsql -Sql $Sql)
    }
    catch {
        throw "PostgreSQL EXPLAIN failed for '$Name': $($_.Exception.Message)"
    }

    foreach ($line in $planLines) {
        $Report.Add([string]$line)
    }

    $Report.Add('```')
    $Report.Add('')
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Required command 'docker' was not found."
}

if (-not (Test-Path -LiteralPath $ComposeFile -PathType Leaf)) {
    throw "Compose file was not found: $ComposeFile"
}

if (-not [string]::IsNullOrWhiteSpace($EnvFile) -and
    -not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
    throw "Environment file was not found: $EnvFile"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssZ')
    $OutputPath = Join-Path $PSScriptRoot "..\artifacts\v6\query-plans-$timestamp.md"
}

if ([string]::IsNullOrWhiteSpace($ApplicationVersion)) {
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $gitSha = (& git rev-parse HEAD 2>$null | Select-Object -First 1)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitSha)) {
            $ApplicationVersion = $gitSha.Trim()
        }
    }

    if ([string]::IsNullOrWhiteSpace($ApplicationVersion)) {
        $ApplicationVersion = 'not provided'
    }
}

if ([string]::IsNullOrWhiteSpace($FixtureDescription)) {
    $FixtureDescription = 'not provided'
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$projectIdLiteral = $ProjectId.ToString()
$today = [DateTime]::UtcNow.Date
$nextDay = $today.AddDays(8)
$todayLiteral = $today.ToString('yyyy-MM-dd')
$nextDayLiteral = $nextDay.ToString('yyyy-MM-dd')

$taskCountSql = @"
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT COUNT(*)
FROM "ProjectTasks"
WHERE "ProjectId" = '$projectIdLiteral';
"@

$visibleProjectListSql = @"
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT project.*
FROM "Projects" AS project
WHERE (
    project."OwnerId" = '$UserId'
    OR EXISTS (
        SELECT 1
        FROM "ProjectMembers" AS member
        INNER JOIN "Users" AS member_user
            ON member_user."Id" = member."UserId"
        WHERE member."ProjectId" = project."Id"
          AND member."UserId" = '$UserId'
          AND member_user."IsActive"
    )
)
  AND NOT project."IsArchived"
ORDER BY project."UpdatedAt" DESC;
"@

$taskPageSql = @"
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT task.*, label.*
FROM (
    SELECT task.*
    FROM "ProjectTasks" AS task
    WHERE task."ProjectId" = '$projectIdLiteral'
    ORDER BY task."CreatedAt" DESC
    LIMIT 20
) AS task
LEFT JOIN "ProjectTaskLabels" AS label
    ON task."Id" = label."ProjectTaskId"
ORDER BY task."CreatedAt" DESC, task."Id", label."Id";
"@

$taskDashboardStatsSql = @"
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT
    COUNT(*) AS "Total",
    COUNT(*) FILTER (WHERE "Status" = 'Todo') AS "Todo",
    COUNT(*) FILTER (WHERE "Status" = 'InProgress') AS "InProgress",
    COUNT(*) FILTER (WHERE "Status" = 'Done') AS "Done",
    COUNT(*) FILTER (WHERE "Priority" = 'Low') AS "LowPriority",
    COUNT(*) FILTER (WHERE "Priority" = 'Normal') AS "NormalPriority",
    COUNT(*) FILTER (WHERE "Priority" = 'High') AS "HighPriority"
FROM "ProjectTasks"
WHERE "ProjectId" = '$projectIdLiteral';
"@

$taskDashboardOverdueSql = @"
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT task.*, label.*
FROM (
    SELECT task.*
    FROM "ProjectTasks" AS task
    WHERE task."ProjectId" = '$projectIdLiteral'
      AND task."DueDate" IS NOT NULL
      AND task."DueDate" < '$todayLiteral'
      AND task."Status" <> 'Done'
    ORDER BY task."DueDate"
    LIMIT 10
) AS task
LEFT JOIN "ProjectTaskLabels" AS label
    ON task."Id" = label."ProjectTaskId"
ORDER BY task."DueDate", task."Id", label."Id";
"@

$taskDashboardUpcomingSql = @"
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT task.*, label.*
FROM (
    SELECT task.*
    FROM "ProjectTasks" AS task
    WHERE task."ProjectId" = '$projectIdLiteral'
      AND task."DueDate" IS NOT NULL
      AND task."DueDate" >= '$todayLiteral'
      AND task."DueDate" < '$nextDayLiteral'
      AND task."Status" <> 'Done'
    ORDER BY task."DueDate"
    LIMIT 10
) AS task
LEFT JOIN "ProjectTaskLabels" AS label
    ON task."Id" = label."ProjectTaskId"
ORDER BY task."DueDate", task."Id", label."Id";
"@

$recentActivitySql = @"
EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT
    activity."Id",
    activity."Type",
    activity."Description",
    activity."ActorUserId",
    activity."ProjectTaskId",
    activity."CreatedAt",
    actor."DisplayName"
FROM "ProjectActivities" AS activity
INNER JOIN "Users" AS actor
    ON activity."ActorUserId" = actor."Id"
WHERE activity."ProjectId" = '$projectIdLiteral'
ORDER BY activity."CreatedAt" DESC
LIMIT 5;
"@

$metadataSql = @"
SELECT
    version() AS "PostgreSQLVersion",
    current_database() AS "DatabaseName",
    current_schema() AS "SchemaName",
    pg_size_pretty(pg_database_size(current_database())) AS "DatabaseSize";

SELECT
    (SELECT COUNT(*) FROM "Users") AS "Users",
    (SELECT COUNT(*) FROM "Projects") AS "Projects",
    (SELECT COUNT(*) FROM "ProjectTasks") AS "ProjectTasks",
    (SELECT COUNT(*) FROM "ProjectTaskLabels") AS "ProjectTaskLabels",
    (SELECT COUNT(*) FROM "ProjectActivities") AS "ProjectActivities";

SELECT
    (SELECT COUNT(*) FROM "ProjectTasks" WHERE "ProjectId" = '$projectIdLiteral') AS "SelectedProjectTasks",
    (SELECT COUNT(*)
     FROM "ProjectTaskLabels" AS label
     INNER JOIN "ProjectTasks" AS task ON task."Id" = label."ProjectTaskId"
     WHERE task."ProjectId" = '$projectIdLiteral') AS "SelectedProjectTaskLabels",
    (SELECT COUNT(*) FROM "ProjectActivities" WHERE "ProjectId" = '$projectIdLiteral') AS "SelectedProjectActivities";
"@

$report = [System.Collections.Generic.List[string]]::new()
$report.Add('# V6 PostgreSQL query plans')
$report.Add('')
$report.Add("- Generated UTC: $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))")
$report.Add("- Application version: $ApplicationVersion")
$report.Add("- Fixture description: $FixtureDescription")
$report.Add("- Project ID: $ProjectId")
$report.Add("- User ID: $(if ($UserId -eq [Guid]::Empty) { 'not provided; visible-project plan skipped' } else { $UserId })")
$report.Add("- Compose file: $ComposeFile")
$report.Add("- Database service: $DbService")
$report.Add("- Database name: $DbName")
$report.Add("- Dashboard date window: $todayLiteral through $nextDayLiteral (exclusive)")
$report.Add("- Task page size: 20")
$report.Add("- Dashboard due-task limit: 10")
$report.Add("- Recent activity limit: 5")
$report.Add('')
$report.Add('> This diagnostic runs read-only SELECT statements with `EXPLAIN (ANALYZE, BUFFERS, VERBOSE)`. Run it against a disposable or staging database because `ANALYZE` executes the statements and adds measurable database work.')
$report.Add('')
$report.Add('> The SQL mirrors the current project-task and project-activity read paths. It is diagnostic evidence, not a replacement for the application baseline runner and not a reason to add an index before comparing the plan.')
$report.Add('')

$report.Add('## Database metadata')
$report.Add('')
$report.Add('```text')
$metadataLines = @(Invoke-ComposePsql -Sql $metadataSql)
foreach ($line in $metadataLines) {
    $report.Add($line)
}
$report.Add('```')
$report.Add('')

Add-ExplainPlan -Report $report -Name 'Project task count' -Sql $taskCountSql
if ($UserId -ne [Guid]::Empty) {
    Add-ExplainPlan -Report $report -Name 'Visible project list' -Sql $visibleProjectListSql
}
Add-ExplainPlan -Report $report -Name 'Project task page with labels' -Sql $taskPageSql
Add-ExplainPlan -Report $report -Name 'Project dashboard task statistics' -Sql $taskDashboardStatsSql
Add-ExplainPlan -Report $report -Name 'Project dashboard overdue tasks' -Sql $taskDashboardOverdueSql
Add-ExplainPlan -Report $report -Name 'Project dashboard upcoming tasks' -Sql $taskDashboardUpcomingSql
Add-ExplainPlan -Report $report -Name 'Recent project activity' -Sql $recentActivitySql

$report.Add('## Review checklist')
$report.Add('')
$report.Add('- Compare actual time, planning time, rows removed by filters, and shared/local buffer reads.')
$report.Add('- Record whether the selected index is used naturally; do not disable sequential scans for baseline evidence.')
$report.Add('- Compare the plan with the API latency report using the same fixture and project.')
$report.Add('- Do not change indexes or projections until one measurable bottleneck and a success threshold are documented.')

$report | Set-Content -Path $OutputPath -Encoding utf8
Write-Host "PostgreSQL query-plan report written to $OutputPath"
