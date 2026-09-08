param(
    [string]$Version = "0.9.0-beta.1"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot "artifacts\bin\Mediance.AcrylicProbe\release_win-x64"
$releaseDirectory = Join-Path $projectRoot "artifacts\release"
$packageName = "Mediance-$Version-win-x64"
$stagingDirectory = Join-Path $releaseDirectory $packageName
$zipPath = Join-Path $releaseDirectory "$packageName.zip"
$checksumPath = Join-Path $releaseDirectory "$packageName-SHA256.txt"

if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory "Mediance.exe"))) {
    throw "Release build was not found at $sourceDirectory. Run .\scripts\dev.ps1 glass-test first."
}

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

New-Item -ItemType Directory -Path $stagingDirectory | Out-Null
Get-ChildItem -LiteralPath $sourceDirectory -Force | Where-Object {
    $_.Extension -ne ".pdb"
} | Copy-Item -Destination $stagingDirectory -Recurse -Force

$packageReadme = @"
Mediance $Version

Requirements: Windows 11 24H2 or newer, x64.

Installation:
1. Extract the complete ZIP archive to a normal folder.
2. Run Mediance.exe.
3. Keep all files in the extracted folder together.

This beta is not code-signed, so Windows SmartScreen may show an unknown publisher warning.

Project: https://github.com/S1lahsizKuvv3t/Mediance
"@
Set-Content -LiteralPath (Join-Path $stagingDirectory "README.txt") -Value $packageReadme -Encoding utf8

Compress-Archive -Path (Join-Path $stagingDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $packageName.zip" -Encoding ascii

$zip = Get-Item -LiteralPath $zipPath
Write-Host "Package: $($zip.FullName)"
Write-Host "Size: $($zip.Length) bytes"
Write-Host "SHA256: $hash"
