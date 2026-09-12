$ErrorActionPreference = 'Stop'
$manifest = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\package\AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifest)) { throw 'Run scripts\Build.ps1 first.' }
Add-AppxPackage -Register $manifest
Write-Host 'Registered Music Controller. Press Win+W and add Music Controller.'
