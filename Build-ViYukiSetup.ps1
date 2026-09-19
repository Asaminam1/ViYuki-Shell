$ErrorActionPreference = "Stop"

$appPublish = Join-Path $PSScriptRoot "artifacts\app-publish"
$hostPublish = Join-Path $PSScriptRoot "artifacts\setup-host"
$packagerPublish = Join-Path $PSScriptRoot "artifacts\packager"
$setupPath = Join-Path $PSScriptRoot "artifacts\ViYukiSetup.exe"

dotnet publish $PSScriptRoot -c Release -r win-x64 --self-contained true --ignore-failed-sources -o $appPublish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $PSScriptRoot "InstallerHost") -c Release -r win-x64 --self-contained true --ignore-failed-sources -o $hostPublish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $PSScriptRoot "Packager") -c Release -r win-x64 --self-contained true --ignore-failed-sources -o $packagerPublish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $packagerPublish "ViYukiPackager.exe") $appPublish (Join-Path $hostPublish "ViYukiSetupHost.exe") $setupPath
exit $LASTEXITCODE
