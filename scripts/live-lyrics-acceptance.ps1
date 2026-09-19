param(
    [ValidateRange(1, 15)][int]$Samples = 12,
    [ValidateRange(30, 420)][int]$AutomaticTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$probe = Join-Path $root 'artifacts\bin\Mediance.MediaProbe\release\Mediance.MediaProbe.dll'
$dotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
$app = Join-Path $root 'artifacts\bin\Mediance.AcrylicProbe\release_win-x64\Mediance.exe'
$log = Join-Path $env:LOCALAPPDATA 'Mediance\prototypes\acrylic.log'
$outDir = Join-Path $root ('artifacts\lyrics-acceptance\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$report = Join-Path $outDir 'report.md'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
if (-not (Test-Path $probe)) { throw 'Build Mediance.MediaProbe first.' }
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
if (-not (Test-Path $app)) { throw 'Build Mediance first.' }
if (-not (Get-Process -Name Mediance -ErrorAction SilentlyContinue | Where-Object Path -eq $app)) {
    Start-Process -FilePath $app -WorkingDirectory $root | Out-Null
    Start-Sleep -Seconds 3
}

function Send-MediaCommand([string]$Command) {
    $ignored = @($Command, 'quit') | & $dotnet $probe interactive 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Media command failed: $Command" }
}

function Read-Lines([int]$Start) {
    if (-not (Test-Path $log)) { return @() }
    return @(Get-Content -LiteralPath $log | Select-Object -Skip $Start)
}

Send-MediaCommand 'play'
Start-Sleep -Seconds 3
$rows = [Collections.Generic.List[object]]::new()
$seen = [Collections.Generic.HashSet[string]]::new()
$trackAttempts = 0

while ($rows.Count -lt $Samples -and $trackAttempts -lt ($Samples + 8)) {
    $trackAttempts++
    $cursor = if (Test-Path $log) { @(Get-Content -LiteralPath $log).Count } else { 0 }
    Send-MediaCommand 'next'
    Start-Sleep -Seconds 4
    $deadline = (Get-Date).AddSeconds(35)
    $loadLine = $null
    while ((Get-Date) -lt $deadline -and -not $loadLine) {
        Start-Sleep -Seconds 1
        $loadLine = Read-Lines $cursor | Where-Object { $_ -match 'LyricsLoadResult:' } | Select-Object -Last 1
    }
    if (-not $loadLine) {
        $rows.Add([pscustomobject]@{ sample = $rows.Count + 1; id = 'unknown'; source = 'No result'; outcome = 'FAIL'; evidence = 'No lyrics result in 35 seconds.' })
        continue
    }
    $id = if ($loadLine -match 'sample=([0-9A-F]{12})') { $Matches[1] } else { 'unknown' }
    if ($id -ne 'unknown' -and -not $seen.Add($id)) { continue }
    $kind = if ($loadLine -match 'kind=([A-Za-z]+)') { $Matches[1] } else { 'Unknown' }
    $local = $loadLine -match 'local=True'

    if ($kind -eq 'Synced') {
        $rows.Add([pscustomobject]@{ sample = $rows.Count + 1; id = $id; source = $(if($local){'Local timing'}else{'Source timed'}); outcome = 'PASS'; evidence = 'A timed document loaded.' })
        continue
    }
    if ($kind -in @('Unavailable','Instrumental')) {
        $rows.Add([pscustomobject]@{ sample = $rows.Count + 1; id = $id; source = $kind; outcome = 'EXPECTED'; evidence = 'No fabricated timing was shown.' })
        continue
    }
    if ($kind -ne 'Plain') {
        $rows.Add([pscustomobject]@{ sample = $rows.Count + 1; id = $id; source = $kind; outcome = 'FAIL'; evidence = 'Unexpected lyrics state.' })
        continue
    }

    $autoDeadline = (Get-Date).AddSeconds($AutomaticTimeoutSeconds)
    $outcome = 'PENDING'
    $evidence = 'Automatic synchronization timed out.'
    while ((Get-Date) -lt $autoDeadline) {
        Start-Sleep -Seconds 2
        $new = Read-Lines $cursor
        if ($new | Where-Object { $_ -match 'LyricsAutoSyncSaved:' }) {
            $outcome = 'PASS'
            $evidence = 'Automatic timing passed confidence checks and was saved.'
            break
        }
        $results = @($new | Where-Object { $_ -match 'LyricsAutoSyncResult:' })
        if ($results.Count -ge 3) {
            $outcome = 'FAIL'
            $evidence = 'Three automatic attempts completed without a reliable alignment.'
            break
        }
        if ($new | Where-Object { $_ -match 'LyricsAutoSync: ' }) {
            $outcome = 'FAIL'
            $evidence = 'Automatic synchronization raised a local processing error.'
            break
        }
    }
    $rows.Add([pscustomobject]@{ sample = $rows.Count + 1; id = $id; source = 'Plain + automatic'; outcome = $outcome; evidence = $evidence })
}

$pass = @($rows | Where-Object outcome -eq 'PASS').Count
$fail = @($rows | Where-Object outcome -eq 'FAIL').Count
$expected = @($rows | Where-Object outcome -eq 'EXPECTED').Count
$pending = @($rows | Where-Object outcome -eq 'PENDING').Count
$sourceTimed = @($rows | Where-Object source -eq 'Source timed').Count
$automatic = @($rows | Where-Object { $_.source -eq 'Plain + automatic' -and $_.outcome -eq 'PASS' }).Count
$lines = @(
    '# Mediance live lyrics acceptance',
    '',
    "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')",
    '',
    '| # | Anonymous sample | Path | Outcome | Evidence |',
    '|---:|---|---|---:|---|'
)
foreach ($row in $rows) {
    $lines += "| $($row.sample) | $($row.id) | $($row.source) | **$($row.outcome)** | $($row.evidence) |"
}
$lines += ''
$lines += "Summary: $pass passed, $fail failed, $expected expected unsupported/instrumental, $pending pending."
$lines += "Coverage: $sourceTimed source-timed and $automatic automatically aligned."
$lines += ''
$lines += 'This report contains no track title, artist, lyric text, transcript, or captured audio.'
Set-Content -LiteralPath $report -Value $lines -Encoding utf8
Write-Host "Live lyrics report: $report"
Write-Host "Samples=$($rows.Count) Pass=$pass Fail=$fail Expected=$expected Pending=$pending SourceTimed=$sourceTimed Automatic=$automatic"
