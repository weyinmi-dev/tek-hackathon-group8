#requires -Version 7
<#
.SYNOPSIS
  Baseline characterization capture — Phase 4 / Milestone M1.

.DESCRIPTION
  Drives the offline-mode (Ai:Provider=Mock) network-ingestion pipeline with
  fixed fixtures and records ONLY the six parity-contract counts per fixture
  as golden JSON. Re-run after each migration milestone via replay.ps1 and diff
  against these files to prove the pipeline still produces identical numbers.

  The captured values are a property of (code + Mock provider + fixtures),
  independent of how the API process was launched. Start the stack however you
  like — the dev default is Aspire:

      dotnet run --project src/AppHost

  then point this script at the Web.Api base URL (default http://localhost:5000).

.IMPORTANT  Fresh-database requirement
  The network pipeline is content-hash idempotent: re-ingesting the same file
  short-circuits to the ORIGINAL run's counts WITHOUT re-executing the analyzer.
  That means a replay against a database that already contains these fixtures
  would return cached numbers and pass trivially — false confidence.

  Therefore every capture/replay MUST run against a database in which these
  fixtures have NOT yet been ingested. Simplest guarantee: a fresh volume.
      docker compose down -v          # or wipe the Aspire pg data volume
  then bring the stack back up before capturing. See README.md in this folder.

.PARAMETER BaseUrl   Web.Api base URL (no trailing slash).
.PARAMETER OutDir    Where golden JSON is written (default docs/baselines).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl    = "http://localhost:5000",
    [string]$Email      = "tunde.b@telco.lag",
    [string]$Password   = "Telco!2025",
    [string]$FixtureDir = (Join-Path $PSScriptRoot "fixtures"),
    [string]$OutDir     = (Join-Path $PSScriptRoot "../../docs/baselines")
)
$ErrorActionPreference = "Stop"

function Get-AccessToken {
    $body = @{ email = $Email; password = $Password } | ConvertTo-Json
    $resp = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post `
        -ContentType "application/json" -Body $body
    if (-not $resp.accessToken) { throw "Login returned no accessToken." }
    return $resp.accessToken
}

function Invoke-Ingest {
    param([string]$Token, [string]$CsvPath)
    # PowerShell 7 -Form sends multipart/form-data. The key MUST be "file" to
    # match IFormCollection.GetFile("file") in the /api/network/ingest endpoint.
    Invoke-RestMethod -Uri "$BaseUrl/api/network/ingest" -Method Post `
        -Headers @{ Authorization = "Bearer $Token" } `
        -Form @{ file = Get-Item $CsvPath }
}

function Select-ParityFields {
    param($Summary)
    # Only deterministic business counts survive into the golden file. The run id,
    # content hash, dedup flag, per-stage timings and timestamps are all stripped
    # because the migration is allowed to change them — it is NOT allowed to change
    # these seven values.
    [ordered]@{
        eventsParsed         = [int]$Summary.eventsParsed
        anomaliesDetected    = [int]$Summary.anomaliesDetected
        alertsCreated        = [int]$Summary.alertsCreated
        alertsUpdated        = [int]$Summary.alertsUpdated
        optimizationsCreated = [int]$Summary.optimizationsCreated
        topologyChanged      = [bool]$Summary.topologyChanged
        finalStatus          = [string]$Summary.finalStatus
    }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$token = Get-AccessToken
Write-Host "Authenticated as $Email." -ForegroundColor Green

$fixtures = Get-ChildItem -Path $FixtureDir -Filter *.csv | Sort-Object Name
if ($fixtures.Count -eq 0) { throw "No .csv fixtures found in $FixtureDir." }

foreach ($fixture in $fixtures) {
    $name    = [System.IO.Path]::GetFileNameWithoutExtension($fixture.Name)
    $summary = Invoke-Ingest -Token $token -CsvPath $fixture.FullName

    if ($summary.finalStatus -ne "Completed") {
        Write-Warning "$name did not complete: finalStatus=$($summary.finalStatus), reason=$($summary.failureReason)"
    }
    if ($summary.deduplicatedFromPriorRun) {
        throw "$name was DEDUPLICATED — the database already contained this fixture. " +
              "Capture against a fresh database (see .IMPORTANT in this script)."
    }

    $parity  = Select-ParityFields -Summary $summary
    $outPath = Join-Path $OutDir "ingest-$name.json"
    $parity | ConvertTo-Json -Depth 4 | Set-Content -Path $outPath -Encoding utf8
    Write-Host ("captured {0,-24} -> {1}" -f $name, (Resolve-Path $outPath).Path)
}

Write-Host "Baseline capture complete. $($fixtures.Count) fixture(s) written to $OutDir." -ForegroundColor Green
