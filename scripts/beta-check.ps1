param(
    [ValidateRange(0, 480)][int]$SoakMinutes = 0,
    [ValidateRange(10, 15)][int]$ExpectedLyricsSamples = 12,
    [switch]$SkipBuild,
    [switch]$SkipSmoke
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$artifactRoot = Join-Path $projectRoot 'artifacts\beta-check'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$reportDirectory = Join-Path $artifactRoot $stamp
$reportPath = Join-Path $reportDirectory 'report.md'
$jsonPath = Join-Path $reportDirectory 'report.json'
$appPath = Join-Path $projectRoot 'artifacts\bin\Mediance.AcrylicProbe\release_win-x64\Mediance.exe'
$logPath = Join-Path $env:LOCALAPPDATA 'Mediance\prototypes\acrylic.log'
$modelPath = Join-Path $env:LOCALAPPDATA 'Mediance\Models\ggml-small.bin'
$checks = [Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

function Add-Check([string]$Name, [string]$Status, [string]$Evidence) {
    $checks.Add([pscustomobject]@{ name = $Name; status = $Status; evidence = $Evidence })
}

Push-Location $projectRoot
try {
    if (-not $SkipBuild) {
        try { & .\scripts\dev.ps1 build | Out-Host; Add-Check 'Release build' 'PASS' 'Solution built successfully.' }
        catch { Add-Check 'Release build' 'FAIL' $_.Exception.Message }
        try {
            $testOutput = (& .\scripts\dev.ps1 test 2>&1 | Out-String)
            $testOutput | Write-Host
            if ($LASTEXITCODE -eq 0) { Add-Check 'Automated tests' 'PASS' 'Core and reliability tests passed.' }
            else { Add-Check 'Automated tests' 'FAIL' "Exit code $LASTEXITCODE" }
        } catch { Add-Check 'Automated tests' 'FAIL' $_.Exception.Message }
    } else {
        Add-Check 'Release build' 'SKIP' 'Skipped by caller.'
        Add-Check 'Automated tests' 'SKIP' 'Skipped by caller.'
    }

    if (-not $SkipSmoke) {
        try { & .\scripts\dev.ps1 glass-test | Out-Host; Add-Check 'Native window smoke' 'PASS' 'WinUI smoke test passed.' }
        catch { Add-Check 'Native window smoke' 'FAIL' $_.Exception.Message }
    } else { Add-Check 'Native window smoke' 'SKIP' 'Skipped by caller.' }

    $mediaNames = @('Spotify', 'chrome', 'msedge', 'opera', 'foobar2000', 'vlc')
    $runningMedia = Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $mediaNames -contains $_.ProcessName } |
        Select-Object -ExpandProperty ProcessName -Unique
    if ($runningMedia) { Add-Check 'Media source presence' 'INFO' ($runningMedia -join ', ') }
    else { Add-Check 'Media source presence' 'PENDING' 'No supported media process was running.' }

    if (Test-Path -LiteralPath $modelPath) {
        $modelBytes = (Get-Item -LiteralPath $modelPath).Length
        if ($modelBytes -ge 400000000) { Add-Check 'Local speech model' 'PASS' "$modelBytes bytes" }
        else { Add-Check 'Local speech model' 'FAIL' 'Model file is incomplete.' }
    } else { Add-Check 'Local speech model' 'PENDING' 'The first-use model has not been downloaded.' }

    $saved = @()
    $attempts = @()
    if (Test-Path -LiteralPath $logPath) {
        $lines = Get-Content -LiteralPath $logPath -ErrorAction SilentlyContinue
        $saved = @($lines | Select-String 'LyricsAutoSyncSaved:' | Select-Object -Last $ExpectedLyricsSamples)
        $attempts = @($lines | Select-String 'LyricsAutoSyncResult:' | Select-Object -Last ($ExpectedLyricsSamples * 3))
    }
    if ($saved.Count -ge $ExpectedLyricsSamples) {
        Add-Check 'Lyrics live acceptance' 'PASS' "$($saved.Count)/$ExpectedLyricsSamples anonymous successful samples found."
    } else {
        Add-Check 'Lyrics live acceptance' 'PENDING' "$($saved.Count)/$ExpectedLyricsSamples successful samples; play varied tracks with lyrics open to complete the gate."
    }
    Add-Check 'Lyrics attempt diagnostics' 'INFO' "$($attempts.Count) recent anonymous attempt records."

    try {
        Add-Type -AssemblyName System.Windows.Forms
        $screens = [Windows.Forms.Screen]::AllScreens
        if ($screens.Count -gt 1) { Add-Check 'Multi-monitor availability' 'INFO' "$($screens.Count) displays detected; edge snap still needs a hands-on pass on each display." }
        else { Add-Check 'Multi-monitor availability' 'PENDING' 'Only one active display was detected.' }
    } catch { Add-Check 'Multi-monitor availability' 'PENDING' 'Display inventory was unavailable.' }

    Add-Check 'Sleep/wake recovery' 'PENDING' 'Manual action required: sleep Windows during playback, resume, and verify the same source recovers.'
    Add-Check 'Source restart recovery' 'PENDING' 'Manual action required: close and reopen Spotify/browser during playback.'
    Add-Check 'Source matrix' 'PENDING' 'Run Spotify, YouTube Music in Chrome/Opera, and one local player; verify metadata, controls, seek, artwork, and lyrics.'

    if ($SoakMinutes -gt 0) {
        if (-not (Test-Path -LiteralPath $appPath)) {
            Add-Check 'Soak run' 'FAIL' 'Built Mediance.exe was not found.'
        } else {
            $existing = Get-Process -Name Mediance -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -eq $appPath }
            if ($existing) {
                Add-Check 'Soak run' 'SKIP' 'The built app was already running; close it before a controlled soak.'
            } else {
                $process = Start-Process -FilePath $appPath -ArgumentList '--background' -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
                $deadline = (Get-Date).AddMinutes($SoakMinutes)
                $alive = $true
                while ((Get-Date) -lt $deadline) {
                    Start-Sleep -Seconds 5
                    if ($process.HasExited) { $alive = $false; break }
                }
                if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
                if ($alive) { Add-Check 'Soak run' 'PASS' "Process remained alive for $SoakMinutes minute(s)." }
                else { Add-Check 'Soak run' 'FAIL' "Process exited early with code $($process.ExitCode)." }
            }
        }
    } else { Add-Check 'Soak run' 'PENDING' 'Run with -SoakMinutes 240 for the four-hour gate.' }
}
finally { Pop-Location }

$pass = @($checks | Where-Object status -eq 'PASS').Count
$fail = @($checks | Where-Object status -eq 'FAIL').Count
$pending = @($checks | Where-Object status -in @('PENDING', 'SKIP')).Count
$lines = @(
    '# Mediance beta quality report',
    '',
    "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')",
    '',
    "| Check | Status | Evidence |",
    "|---|---:|---|"
)
foreach ($check in $checks) {
    $evidence = $check.evidence -replace '\|', '\|'
    $lines += "| $($check.name) | **$($check.status)** | $evidence |"
}
$lines += ''
$lines += "Automated summary: $pass passed, $fail failed, $pending pending or skipped."
$lines += ''
$lines += 'A release candidate is not accepted while any FAIL remains. PENDING live checks stay visible and are never counted as passes.'
Set-Content -LiteralPath $reportPath -Value $lines -Encoding utf8
[pscustomobject]@{ generatedUtc = [DateTimeOffset]::UtcNow; checks = $checks } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonPath -Encoding utf8
Write-Host "Beta report: $reportPath"
if ($fail -gt 0) { exit 1 }
