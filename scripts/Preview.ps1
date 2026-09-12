param([switch]$Sample)
$ErrorActionPreference = 'Stop'
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\package\Desktop\MusicWidget.Desktop.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run scripts\Build.ps1 first.' }
if ($Sample) { Start-Process -FilePath $exe -ArgumentList '--sample' } else { Start-Process -FilePath $exe }
