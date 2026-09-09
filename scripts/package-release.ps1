param(
    [string]$Version = "0.9.0-beta.2"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot "artifacts\bin\Mediance.AcrylicProbe\release_win-x64"
$releaseDirectory = Join-Path $projectRoot "artifacts\release"
$launcherProject = Join-Path $projectRoot "packaging\Mediance.Launcher\Mediance.Launcher.csproj"
$packageName = "Mediance-$Version-win-x64"
$stagingDirectory = Join-Path $releaseDirectory $packageName
$applicationDirectory = Join-Path $stagingDirectory "App"
$documentationDirectory = Join-Path $stagingDirectory "Documentation"
$launcherDirectory = Join-Path $releaseDirectory ".launcher-$Version"
$zipPath = Join-Path $releaseDirectory "$packageName.zip"
$checksumPath = Join-Path $releaseDirectory "$packageName-SHA256.txt"

$bundledDotnet = Join-Path $projectRoot ".tools\dotnet\dotnet.exe"
if (Test-Path -LiteralPath $bundledDotnet) {
    $dotnet = $bundledDotnet
}
else {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

if (-not (Test-Path -LiteralPath (Join-Path $sourceDirectory "Mediance.exe"))) {
    throw "Release build was not found at $sourceDirectory. Run .\scripts\dev.ps1 glass-test first."
}

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
foreach ($path in @($stagingDirectory, $launcherDirectory)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
foreach ($path in @($zipPath, $checksumPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

New-Item -ItemType Directory -Path $applicationDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $documentationDirectory -Force | Out-Null

Get-ChildItem -LiteralPath $sourceDirectory -Force | Where-Object {
    $_.Extension -ne ".pdb" -and
    $_.Name -notin @("LICENSE.txt", "THIRD_PARTY_NOTICES.md")
} | Copy-Item -Destination $applicationDirectory -Recurse -Force

Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination (Join-Path $documentationDirectory "LICENSE.txt")
Copy-Item -LiteralPath (Join-Path $projectRoot "THIRD_PARTY_NOTICES.md") -Destination $documentationDirectory

$packageReadme = @"
Mediance $Version

Requirements: Windows 11 24H2 or newer, x64.

Installation:
1. Extract the complete ZIP archive to a normal folder.
2. Run Mediance.exe from the top level of the extracted folder.
3. Keep the App folder beside Mediance.exe. It contains the files the application needs.

This beta is not code-signed, so Windows SmartScreen may show an unknown publisher warning.

Project: https://github.com/S1lahsizKuvv3t/Mediance
"@
Set-Content -LiteralPath (Join-Path $documentationDirectory "README.txt") -Value $packageReadme -Encoding utf8

& $dotnet publish $launcherProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $launcherDirectory `
    -p:Version=$Version `
    -p:AssemblyVersion=0.9.0.0 `
    -p:FileVersion=0.9.0.0 `
    -p:InformationalVersion=$Version `
    --nologo
if ($LASTEXITCODE -ne 0) {
    throw "The Mediance launcher build failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath (Join-Path $launcherDirectory "Mediance.exe") -Destination $stagingDirectory

$rootFiles = @(Get-ChildItem -LiteralPath $stagingDirectory -File)
if ($rootFiles.Count -ne 1 -or $rootFiles[0].Name -ne "Mediance.exe") {
    throw "The package root must contain only Mediance.exe."
}

Compress-Archive -Path (Join-Path $stagingDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $packageName.zip" -Encoding ascii

$zip = Get-Item -LiteralPath $zipPath
Write-Host "Package: $($zip.FullName)"
Write-Host "Size: $($zip.Length) bytes"
Write-Host "SHA256: $hash"
