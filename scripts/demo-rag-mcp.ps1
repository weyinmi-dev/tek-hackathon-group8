﻿<#
.SYNOPSIS
  End-to-end smoke test for TelcoPilot's RAG and MCP surfaces.

.DESCRIPTION
  Walks Phases 1-6 from docs:
    1. RAG seed sanity         -- corpus seeded, all docs Indexed
    2. RAG retrieval quality   -- chat answers cite the right SourceKeys
    3. RAG ingestion + reindex -- upload a fresh doc, query for its unique fact
    4. MCP discovery           -- /mcp/plugins lists the wired plugins
    5. MCP invocation          -- every capability returns IsSuccess=true
    6. RBAC negatives          -- viewer cannot list plugins, engineer cannot invoke or upload

  Each check prints PASS / FAIL and the script exits non-zero if any check failed.

.PARAMETER BaseUrl
  Origin to hit. Defaults to http://localhost:3000 (the Next.js dev server proxies /api
  to the Aspire-hosted Web.Api). Use the Web.Api endpoint directly if you want to skip
  the Next.js rewrite.

.PARAMETER EngineerEmail / ManagerEmail / AdminEmail / ViewerEmail
  Override the seeded demo accounts if you've changed the seeder.

.PARAMETER Password
  Default demo password. Override only if you've changed it.

.PARAMETER SkipUpload
  Skip Phase 3 (upload + reindex). Useful when you've run the script before and don't
  want yet another "TWR-IKJ-099 decommissioning notice" doc cluttering the corpus.

.EXAMPLE
  pwsh ./scripts/demo-rag-mcp.ps1
  pwsh ./scripts/demo-rag-mcp.ps1 -BaseUrl http://localhost:5000 -SkipUpload
#>

[CmdletBinding()]
param(
    [string] $BaseUrl       = 'http://localhost:3000',
    [string] $EngineerEmail = 'oluwaseun.a@telco.lag',
    [string] $ManagerEmail  = 'amaka.o@telco.lag',
    [string] $AdminEmail    = 'tunde.b@telco.lag',
    [string] $ViewerEmail   = 'kemi.a@telco.lag',
    [string] $Password      = 'Telco!2025',
    [switch] $SkipUpload
)

$ErrorActionPreference = 'Stop'

# ---------- pretty-print + pass/fail tracking -----------------------------------------------------

$script:Failures = @()
$script:Passes   = 0

function Write-Section($title) {
    Write-Host ''
    Write-Host ('=' * 80) -ForegroundColor DarkGray
    Write-Host $title -ForegroundColor Cyan
    Write-Host ('=' * 80) -ForegroundColor DarkGray
}

function Assert-That($name, [scriptblock] $check, $detail = '') {
    try {
        $result = & $check
        if ($result) {
            Write-Host "  PASS  $name" -ForegroundColor Green
            $script:Passes++
        } else {
            Write-Host "  FAIL  $name" -ForegroundColor Red
            if ($detail) { Write-Host "        $detail" -ForegroundColor DarkYellow }
            $script:Failures += $name
        }
    } catch {
        Write-Host "  FAIL  $name (threw: $($_.Exception.Message))" -ForegroundColor Red
        if ($detail) { Write-Host "        $detail" -ForegroundColor DarkYellow }
        $script:Failures += $name
    }
}

# ---------- HTTP helpers --------------------------------------------------------------------------

function Invoke-Login([string] $email) {
    $body = @{ email = $email; password = $Password } | ConvertTo-Json -Compress
    $r = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" `
        -ContentType 'application/json' -Body $body
    return $r.accessToken
}

function Api([string] $token, [string] $method, [string] $path, $body = $null) {
    $headers = @{ Authorization = "Bearer $token" }
    if ($null -ne $body) {
        return Invoke-RestMethod -Method $method -Uri "$BaseUrl$path" -Headers $headers `
            -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 6 -Compress)
    }
    return Invoke-RestMethod -Method $method -Uri "$BaseUrl$path" -Headers $headers
}

# Status-code-only -- used for RBAC negatives where we *expect* a non-2xx.
function Api-Status([string] $token, [string] $method, [string] $path, $body = $null) {
    $headers = @{ Authorization = "Bearer $token" }
    $args = @{
        Method                = $method
        Uri                   = "$BaseUrl$path"
        Headers               = $headers
        SkipHttpErrorCheck    = $true
    }
    if ($null -ne $body) {
        $args.ContentType = 'application/json'
        $args.Body        = ($body | ConvertTo-Json -Depth 6 -Compress)
    }
    return (Invoke-WebRequest @args).StatusCode
}

# ---------- bootstrap -----------------------------------------------------------------------------

Write-Section "Logging in (engineer / manager / admin / viewer)"
$tokens = @{}
try {
    $tokens.engineer = Invoke-Login $EngineerEmail
    $tokens.manager  = Invoke-Login $ManagerEmail
    $tokens.admin    = Invoke-Login $AdminEmail
    $tokens.viewer   = Invoke-Login $ViewerEmail
    Write-Host "  PASS  obtained 4 access tokens" -ForegroundColor Green
    $script:Passes++
} catch {
    Write-Host "  FAIL  could not log in -- backend reachable at $BaseUrl ?  ($($_.Exception.Message))" -ForegroundColor Red
    Write-Host ''
    Write-Host "Hint: start the AppHost with 'dotnet run --project src/AppHost' and confirm" -ForegroundColor DarkYellow
    Write-Host "      the Next.js frontend is up on $BaseUrl, then re-run this script." -ForegroundColor DarkYellow
    exit 2
}

# ---------- Phase 1 -- RAG seed sanity -------------------------------------------------------------

Write-Section "Phase 1 -- RAG seed sanity"

$docs = Api $tokens.engineer 'GET' '/api/documents'
Write-Host "  /api/documents returned $($docs.Count) docs" -ForegroundColor DarkGray

Assert-That "corpus has at least 13 seeded documents" { $docs.Count -ge 13 } `
    "Got $($docs.Count). Either the seeder hasn't run or the DB was wiped without restarting the API."

$pending = $docs | Where-Object { $_.status -ne 'Indexed' }
Assert-That "every document is in status=Indexed" { -not $pending } `
    "Pending docs: $(($pending | ForEach-Object { $_.title }) -join ', ')"

# ---------- Phase 2 -- RAG retrieval quality -------------------------------------------------------

Write-Section "Phase 2 -- RAG retrieval quality (chat answers cite expected SourceKeys)"

# Each row: { query; expected SourceKeys (any-of); friendly label }
$ragQueries = @(
    @{ q='Why did Lekki Phase 1 go down?';            any=@('INC-2841-WRITEUP','OUTAGE-LEKKI-CORRIDOR-2025'); label='Lekki Phase 1 outage' },
    @{ q="What's the SOP for a fiber cut?";           any=@('SOP-FIBER-CUT-V3');                              label='Fiber-cut SOP' },
    @{ q='Lagos West grid failure recovery';          any=@('INC-2840-WRITEUP','SOP-POWER-FAILOVER-V2');      label='Lagos West grid failure' },
    @{ q='Predict failures for Lagos West towers';    any=@('TOWER-PERF-LAG-W-031');                          label='Lagos West thermal trend' },
    @{ q='When does Festac get congested?';           any=@('DIAG-CONGESTION-PATTERNS','ALERT-HISTORY-FESTAC-2025'); label='Festac congestion' }
)

foreach ($t in $ragQueries) {
    $answer    = Api $tokens.engineer 'POST' '/api/chat' @{ query = $t.q; conversationId = $null }
    $body      = $answer.answer
    $hit       = $t.any | Where-Object { $body -match [regex]::Escape($_) }
    $matched   = $hit -join ', '
    $expected  = $t.any -join ', '
    $headLen   = [Math]::Min(180, $body.Length)
    $head      = $body.Substring(0, $headLen)
    $checkName = '[' + $t.label + '] answer cites at least one of: ' + $expected
    $detail    = 'Got SourceKeys hit: ' + $matched + '.  Answer head: ' + $head + '...'
    Assert-That $checkName { [bool]$hit } $detail
}

# ---------- Phase 3 -- RAG ingestion + reindex -----------------------------------------------------

Write-Section "Phase 3 -- RAG ingestion + reindex (manager)"

if ($SkipUpload) {
    Write-Host "  SKIP  (-SkipUpload supplied)" -ForegroundColor DarkYellow
}
if (-not $SkipUpload) {
    # The unique fact is the marker we'll grep for. Use a non-trivial token sequence so the
    # hashing embedder can find it.
    $uniqueId = "TWR-IKJ-099-$(Get-Random -Minimum 1000 -Maximum 9999)"
    $bodyText = "$uniqueId was decommissioned on 2026-04-15 due to Allen Avenue zoning revisions.`nReplacement is TWR-IKJ-104, hot-failover already verified."
    $tmpFile = Join-Path ([System.IO.Path]::GetTempPath()) "telcopilot-decom-$uniqueId.txt"
    Set-Content -Path $tmpFile -Value $bodyText -Encoding utf8

    # multipart/form-data upload via -Form (PowerShell 7+).
    $form = @{
        file     = Get-Item $tmpFile
        title    = "$uniqueId decommissioning notice"
        category = 'IncidentReport'
        region   = 'Ikeja'
        tags     = "decommission,ikeja,$uniqueId"
    }
    try {
        $uploaded = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/documents/upload" `
            -Headers @{ Authorization = "Bearer $($tokens.manager)" } -Form $form
    } finally {
        Remove-Item $tmpFile -ErrorAction SilentlyContinue
    }

    Assert-That "upload returned a document id" { $uploaded.id } `
        "Upload response: $($uploaded | ConvertTo-Json -Compress)"

    if ($uploaded.id) {
        # Give the indexer a heartbeat to chunk + embed. Indexing is synchronous in the upload
        # handler today, so this is belt-and-braces.
        Start-Sleep -Milliseconds 500

        $listed = Api $tokens.manager 'GET' '/api/documents'
        $row    = $listed | Where-Object { $_.id -eq $uploaded.id }
        Assert-That "uploaded doc shows up in /documents with status=Indexed" {
            $row -and $row.status -eq 'Indexed'
        } "Row: $($row | ConvertTo-Json -Compress)"

        $answer = Api $tokens.engineer 'POST' '/api/chat' @{ query = "What happened to $uniqueId?"; conversationId = $null }
        $sourceKey = if ($uploaded.sourceKey) { $uploaded.sourceKey } else { '' }
        Assert-That "chat answer for $uniqueId cites the new document SourceKey" {
            ($sourceKey -and ($answer.answer -match [regex]::Escape($sourceKey))) `
                -or ($answer.answer -match [regex]::Escape($uniqueId))
        } "Answer head: $($answer.answer.Substring(0,[Math]::Min(220,$answer.answer.Length)))..."

        # Reindex -- should be idempotent and end at Indexed.
        $reindexStatus = Api-Status $tokens.manager 'POST' "/api/documents/$($uploaded.id)/reindex"
        Assert-That "reindex returns 204 (NoContent)" { $reindexStatus -eq 204 } "Got HTTP $reindexStatus"

        Start-Sleep -Milliseconds 500
        $listed = Api $tokens.manager 'GET' '/api/documents'
        $row    = $listed | Where-Object { $_.id -eq $uploaded.id }
        Assert-That "doc returns to status=Indexed after reindex" {
            $row -and $row.status -eq 'Indexed'
        } "Row: $($row | ConvertTo-Json -Compress)"
    }
}

# ---------- Phase 4 -- MCP discovery ---------------------------------------------------------------

Write-Section "Phase 4 -- MCP discovery"

$plugins = Api $tokens.engineer 'GET' '/api/mcp/plugins'
$pluginIds = ($plugins | ForEach-Object { $_.pluginId }) -join ', '
Write-Host "  pluginIds: $pluginIds" -ForegroundColor DarkGray

Assert-That "/mcp/plugins lists the network plugin" { $plugins | Where-Object { $_.pluginId -eq 'network' } }
Assert-That "/mcp/plugins lists the alerts plugin"  { $plugins | Where-Object { $_.pluginId -eq 'alerts'  } }

$capCount = ($plugins | ForEach-Object { $_.capabilities.Count } | Measure-Object -Sum).Sum
Assert-That "plugins expose 5 capabilities total (3 network + 2 alerts)" { $capCount -eq 5 } `
    "Got $capCount capabilities"

# ---------- Phase 5 -- MCP invocation --------------------------------------------------------------

Write-Section "Phase 5 -- MCP invocation (manager)"

$invocations = @(
    @{ pluginId='network'; capability='list_towers';    args=@{} },
    @{ pluginId='network'; capability='list_by_region'; args=@{ region='Lekki' } },
    @{ pluginId='network'; capability='region_health';  args=@{} },
    @{ pluginId='alerts';  capability='list_active';    args=@{} },
    @{ pluginId='alerts';  capability='list_all';       args=@{} }
)

foreach ($inv in $invocations) {
    $payload = @{ pluginId = $inv.pluginId; capability = $inv.capability; arguments = $inv.args }
    $r = Api $tokens.manager 'POST' '/api/mcp/invoke' $payload
    Assert-That "$($inv.pluginId)/$($inv.capability) -- isSuccess=true with non-null output" {
        $r.isSuccess -eq $true -and $null -ne $r.output
    } "Result: $($r | ConvertTo-Json -Compress -Depth 4)"
}

# Lekki filter sanity -- every returned tower should sit in the Lekki region.
$lekki = Api $tokens.manager 'POST' '/api/mcp/invoke' @{
    pluginId='network'; capability='list_by_region'; arguments=@{ region='Lekki' }
}
$nonLekki = $lekki.output | Where-Object { $_.region -ne 'Lekki' }
Assert-That "list_by_region(Lekki) only returns Lekki towers" { -not $nonLekki } `
    "Off-region rows: $($nonLekki | ConvertTo-Json -Compress)"

# Unknown capability -- should surface as a structured failure (NOT a 500).
$bad = Api $tokens.manager 'POST' '/api/mcp/invoke' @{
    pluginId='network'; capability='does_not_exist'
}
Assert-That "unknown capability returns isSuccess=false with a clear error" {
    $bad.isSuccess -eq $false -and $bad.error
} "Result: $($bad | ConvertTo-Json -Compress)"

# Audit trail -- each successful invoke must produce an mcp.invoke:* row.
$audit = Api $tokens.manager 'GET' '/api/metrics/audit?take=50'
$mcpRows = $audit | Where-Object { $_.action -like 'mcp.invoke:*' }
Assert-That "audit log contains at least 5 mcp.invoke entries from this run" { $mcpRows.Count -ge 5 } `
    "Got $($mcpRows.Count) mcp.invoke rows in the last 50 audit entries."

# ---------- Phase 6 -- RBAC negatives --------------------------------------------------------------

Write-Section "Phase 6 -- RBAC negatives (these calls SHOULD be rejected)"

$status = Api-Status $tokens.viewer 'GET' '/api/mcp/plugins'
Assert-That "viewer cannot GET /mcp/plugins (engineer+ only)" { $status -eq 403 } "Got HTTP $status"

$status = Api-Status $tokens.engineer 'POST' '/api/mcp/invoke' @{
    pluginId='network'; capability='list_towers'
}
Assert-That "engineer cannot POST /mcp/invoke (manager+ only)" { $status -eq 403 } "Got HTTP $status"

# Engineer attempting an upload -- we don't actually need to send a file body, the role check
# happens before the form parser runs.
$status = Api-Status $tokens.engineer 'POST' '/api/documents/upload'
Assert-That "engineer cannot POST /documents/upload (manager+ only)" { $status -eq 403 } "Got HTTP $status"

# ---------- summary -------------------------------------------------------------------------------

Write-Host ''
Write-Host ('=' * 80) -ForegroundColor DarkGray
if ($script:Failures.Count -eq 0) {
    Write-Host "ALL GREEN -- $($script:Passes) checks passed." -ForegroundColor Green
    exit 0
}
Write-Host "FAILED: $($script:Failures.Count) of $($script:Passes + $script:Failures.Count) checks" -ForegroundColor Red
foreach ($f in $script:Failures) { Write-Host ('  - ' + $f) -ForegroundColor Red }
exit 1
