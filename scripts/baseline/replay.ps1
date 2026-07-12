#requires -Version 7
<#
.SYNOPSIS
  Baseline characterization replay — Phase 4 / Milestone M1.

.DESCRIPTION
  Re-captures the parity-contract counts against the CURRENT build and diffs
  them against the committed golden files in docs/baselines. Exit code 0 means
  the pipeline still produces identical numbers; non-zero means a milestone
  changed observable pipeline output and must be reviewed.

  Run this after each cutover milestone (M3, M9, M11, M12) against a FRESH
  database — see capture.ps1's .IMPORTANT note on content-hash dedup.

.PARAMETER BaseUrl   Web.Api base URL (no trailing slash).
#>
[CmdletBinding()]
param(
    [string]$BaseUrl     = "http://localhost:5000",
    [string]$BaselineDir = (Join-Path $PSScriptRoot "../../docs/baselines")
)
$ErrorActionPreference = "Stop"

if (-not (Test-Path $BaselineDir) -or -not (Get-ChildItem $BaselineDir -Filter *.json)) {
    throw "No golden files in $BaselineDir. Run capture.ps1 first to establish the baseline."
}

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("baseline-replay-" + [System.Guid]::NewGuid().ToString("N"))
& (Join-Path $PSScriptRoot "capture.ps1") -BaseUrl $BaseUrl -OutDir $tmp | Out-Null

$failed = $false
foreach ($golden in Get-ChildItem $BaselineDir -Filter *.json | Sort-Object Name) {
    $replayPath = Join-Path $tmp $golden.Name
    if (-not (Test-Path $replayPath)) {
        Write-Host "MISSING  $($golden.Name) — fixture produced no result this run." -ForegroundColor Red
        $failed = $true
        continue
    }
    $goldenText = (Get-Content $golden.FullName -Raw).Trim()
    $replayText = (Get-Content $replayPath   -Raw).Trim()
    if ($goldenText -ceq $replayText) {
        Write-Host "MATCH    $($golden.Name)" -ForegroundColor Green
    }
    else {
        Write-Host "DIFF     $($golden.Name)" -ForegroundColor Red
        Write-Host "  baseline: $goldenText"
        Write-Host "  current : $replayText"
        $failed = $true
    }
}

Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue

if ($failed) {
    Write-Host "PARITY BROKEN — review the diffs above before proceeding." -ForegroundColor Red
    exit 1
}
Write-Host "PARITY HELD — pipeline counts match the baseline." -ForegroundColor Green
