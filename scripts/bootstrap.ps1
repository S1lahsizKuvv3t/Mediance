$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sdkVersion = (Get-Content -LiteralPath (Join-Path $projectRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
$sdkDirectory = Join-Path $projectRoot '.tools\dotnet'
$workDirectory = Join-Path $projectRoot 'work'
New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
$installerPath = Join-Path $workDirectory 'dotnet-install.ps1'
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installerPath
& $installerPath -Version $sdkVersion -InstallDir $sdkDirectory -NoPath
if (!(Test-Path -LiteralPath (Join-Path $sdkDirectory 'dotnet.exe'))) {
    throw 'Local .NET SDK installation did not finish.'
}
