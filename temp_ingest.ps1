$ErrorActionPreference = 'Stop'

try {
    Write-Host "Logging in as david.u@telco.lag..."
    $loginBody = @{ Email = 'david.u@telco.lag'; Password = 'Telco!2025' } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri 'http://localhost:5000/api/auth/login' -Method Post -Body $loginBody -ContentType 'application/json'
    $token = $login.AccessToken
    Write-Host 'Access token acquired.'

    $csv = @"
timestamp,tower_code,signal_pct,load_pct,latency_ms,status
2026-05-05T08:00:00Z,LOS-T-014,98,42,18,OK
2026-05-05T08:05:00Z,ABV-T-007,80,60,,
"@

    $temp = Join-Path $PWD 'sample_ingest.csv'
    Set-Content -Path $temp -Value $csv -Encoding utf8
    Write-Host "Created sample file: $temp"

    Write-Host 'Posting to /api/network/ingest...'
    $response = Invoke-RestMethod -Uri 'http://localhost:5000/api/network/ingest' -Headers @{ Authorization = "Bearer $token" } -Method Post -Form @{ file = Get-Item $temp }

    Write-Host '--- RESPONSE START ---'
    $response | ConvertTo-Json -Depth 5 | Write-Host
    Write-Host '--- RESPONSE END ---'

} catch {
    Write-Host 'ERROR:'
    $_ | Format-List -Force
    exit 1
}
