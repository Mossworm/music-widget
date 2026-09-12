$ErrorActionPreference = 'Stop'
Get-AppxPackage -Name 'Mossworm.MusicWidget' | Remove-AppxPackage
Write-Host 'Music Controller unregistered.'
