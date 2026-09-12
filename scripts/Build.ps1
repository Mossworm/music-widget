param([switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'artifacts'
$stage = Join-Path $output 'package'
New-Item -ItemType Directory -Force -Path $stage | Out-Null
foreach ($name in @('Desktop', 'Provider')) {
    $project = Join-Path $projectRoot "src\MusicWidget.$name\MusicWidget.$name.csproj"
    $argsList = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', (Join-Path $stage $name))
    if ($SkipRestore) { $argsList += '--no-restore' }
    & dotnet @argsList
    if ($LASTEXITCODE -ne 0) { throw "$name publish failed" }
}
& (Join-Path $PSScriptRoot 'New-Assets.ps1') -Destination (Join-Path $stage 'Assets')
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\AppxManifest.xml') -Destination $stage
# Remove the former Korean resource from incremental build output as well.
$legacyResource = Join-Path $stage 'Strings\ko-KR\Resources.resw'
if (Test-Path -LiteralPath $legacyResource) { Remove-Item -LiteralPath $legacyResource -Force }
$legacyLanguage = Join-Path $stage 'Strings\ko-KR'
if ((Test-Path -LiteralPath $legacyLanguage) -and !(Get-ChildItem -LiteralPath $legacyLanguage -Force)) { Remove-Item -LiteralPath $legacyLanguage }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Strings') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\Strings\en-US') -Destination (Join-Path $stage 'Strings') -Recurse -Force
# WinRT resolves widget interface metadata from the package root when the host
# marshals provider callbacks. Keeping it only beside the EXE fails with 0x8000000F.
Copy-Item -LiteralPath (Join-Path $stage 'Provider\Microsoft.Windows.Widgets.winmd') -Destination $stage
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Public'), (Join-Path $stage 'Provider\Assets') | Out-Null
$exe = Join-Path $stage 'Desktop\MusicWidget.Desktop.exe'
$process = Start-Process -FilePath $exe -ArgumentList @('--sample', '--render', ('"' + (Join-Path $stage 'Assets') + '"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw 'Preview rendering failed' }
$sdk = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } | Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdk) { throw 'Windows SDK makeappx.exe is required.' }
& (Join-Path $sdk.FullName 'x64\makepri.exe') new /pr $stage /cf (Join-Path $projectRoot 'packaging\priconfig.xml') /of (Join-Path $stage 'resources.pri') /o
if ($LASTEXITCODE -ne 0) { throw 'Resource indexing failed' }
& (Join-Path $sdk.FullName 'x64\makeappx.exe') pack /d $stage /p (Join-Path $output 'MusicWidget.msix') /o | Out-File (Join-Path $output 'packaging.log')
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed' }
Write-Host "Built: $output\MusicWidget.msix"

