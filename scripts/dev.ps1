param(
    [ValidateSet('build', 'test', 'snapshot', 'watch', 'media', 'audio', 'lyrics', 'glass', 'glass-test')]
    [string]$Action = 'build',
    [ValidateRange(1, 3600)][int]$Seconds = 30
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnetExe = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
$artifactsPath = Join-Path $projectRoot 'artifacts'
$glassExe = Join-Path $artifactsPath 'bin\Mediance.AcrylicProbe\release_win-x64\Mediance.exe'
Push-Location $projectRoot
try {
    switch ($Action) {
        'build' { & $dotnetExe build Mediance.slnx --configuration Release --artifacts-path $artifactsPath --nologo }
        'test' { & $dotnetExe test tests/Mediance.Core.Tests --configuration Release --artifacts-path $artifactsPath --nologo }
        'snapshot' { & $dotnetExe run --project prototypes/Mediance.MediaProbe --configuration Release -- snapshot }
        'watch' { & $dotnetExe run --project prototypes/Mediance.MediaProbe --configuration Release -- watch $Seconds }
        'media' { & $dotnetExe run --project prototypes/Mediance.MediaProbe --configuration Release -- interactive }
        'audio' { & $dotnetExe run --project prototypes/Mediance.AudioProbe --configuration Release -- inspect Spotify.exe }
        'lyrics' { & $dotnetExe run --project prototypes/Mediance.LyricsProbe --configuration Release }
        { $_ -in 'glass', 'glass-test' } {
            $running = Get-Process -Name Mediance -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -eq $glassExe }
            if ($running) { throw 'Cam pencere zaten açık. Yeni derlemeden önce bu pencereyi kapat.' }
            & $dotnetExe build prototypes/Mediance.AcrylicProbe --configuration Release --artifacts-path $artifactsPath --nologo
            if ($LASTEXITCODE -ne 0) { throw "WinUI build failed (exit $LASTEXITCODE)." }
            if ($Action -eq 'glass-test') {
                $probe = Start-Process -FilePath $glassExe -ArgumentList '--smoke-test' -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -Wait
                if ($probe.ExitCode -ne 0) { throw "Window smoke test failed (exit $($probe.ExitCode))." }
            }
            else { Start-Process -FilePath $glassExe -WorkingDirectory $projectRoot }
        }
    }
    if ($LASTEXITCODE -ne 0) { throw "Mediance action failed (exit $LASTEXITCODE)." }
}
finally { Pop-Location }
