param(
    [string]$OutputDirectory = '',
    [string]$Python = 'python',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'assets\marketing'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$exe = Join-Path $root 'artifacts\bin\Mediance.AcrylicProbe\release_win-x64\Mediance.exe'

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'dev.ps1') build
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}
if (-not (Test-Path $exe)) { throw 'Mediance release build was not found.' }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Get-Process Mediance -ErrorAction SilentlyContinue |
    Where-Object { try { $_.Path -eq $exe } catch { $false } } |
    Stop-Process -Force

$arguments = @(
    '--smoke-test',
    "--preview-dir=$OutputDirectory",
    '--lyrics-preview',
    '--narrow-preview'
)
$process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -Wait
if ($process.ExitCode -ne 0) {
    throw "Mediance preview smoke failed with exit code $($process.ExitCode)."
}

& $Python (Join-Path $PSScriptRoot 'build-marketing-gif.py') $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Marketing GIF generation failed.' }

Write-Host "Marketing pack: $OutputDirectory"
