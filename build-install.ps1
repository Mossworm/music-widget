#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$BuildOnly,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$artifacts = Join-Path $repo 'artifacts'
$build = Join-Path $artifacts '.build'
$publish = Join-Path $artifacts 'publish'
$stage = Join-Path $build 'package'
$package = Join-Path $artifacts 'package'
$backup = Join-Path $build 'previous-package'

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed (exit $LASTEXITCODE)." }
}

function Assert-ArtifactPath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $boundary = [IO.Path]::GetFullPath($artifacts) + [IO.Path]::DirectorySeparatorChar
    if (!$resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to move a path outside artifacts: $resolved"
    }
    if ((Test-Path -LiteralPath $Path) -and ((Get-Item -LiteralPath $Path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to move a linked directory: $resolved"
    }
}

function Stop-WidgetProcesses {
    foreach ($process in @(Get-Process -Name 'MusicWidget.Provider', 'MusicWidget.Desktop' -ErrorAction SilentlyContinue)) {
        # Only stop executables belonging to this checkout.
        if ($process.Path -and $process.Path.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $process.Id -Force
            if (!$process.WaitForExit(10000)) { throw "Process $($process.Id) did not stop." }
        }
    }
}

Push-Location $repo
try {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    if (!$BuildOnly) {
        $developerMode = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue
        if (!$developerMode -or !$developerMode.PSObject.Properties['AllowDevelopmentWithoutDevLicense'] -or $developerMode.AllowDevelopmentWithoutDevLicense -ne 1) {
            throw 'Enable Windows Developer Mode in Settings, then run this script again.'
        }
        $installed = Get-AppxPackage -Name 'Mossworm.MusicWidget'
        if ($installed -and !$installed.IsDevelopmentMode) {
            throw 'A signed installation already exists. This script only replaces development registrations.'
        }
    }

    Assert-ArtifactPath $build
    Assert-ArtifactPath $publish
    if (Test-Path -LiteralPath $build) { Remove-Item -LiteralPath $build -Recurse -Force }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Write-Host 'Building and checking YT Music Controller...'
    Invoke-Checked $dotnet @('build', 'MusicWidget.slnx', '-c', $Configuration, '--nologo')
    Invoke-Checked $dotnet @('run', '--project', 'tests/MusicWidget.Checks', '-c', $Configuration, '--no-build')
    foreach ($app in @('Desktop', 'Provider')) {
        Invoke-Checked $dotnet @('publish', "src/MusicWidget.$app/MusicWidget.$app.csproj", '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-o', (Join-Path $stage $app), '--nologo')
    }
    Copy-Item -LiteralPath (Join-Path $repo 'packaging\AppxManifest.xml') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $repo 'packaging\Assets'), (Join-Path $repo 'packaging\Strings') -Destination $stage -Recurse
    # Out-of-process WinRT marshaling resolves this metadata from the package root.
    # Keeping it only beside the provider EXE lets registration succeed but breaks GetDefault().
    Copy-Item -LiteralPath (Join-Path $stage 'Provider\Microsoft.Windows.Widgets.winmd') -Destination $stage
    New-Item -ItemType Directory -Path (Join-Path $stage 'Public') -Force | Out-Null
    $preview = Start-Process -FilePath (Join-Path $stage 'Desktop\MusicWidget.Desktop.exe') -ArgumentList @('--render', ('"' + (Join-Path $stage 'Assets') + '"')) -WindowStyle Hidden -PassThru
    if (!$preview.WaitForExit(30000)) { $preview.Kill(); throw 'Preview rendering timed out.' }
    if ($preview.ExitCode -ne 0) { throw "Preview rendering failed: $($preview.ExitCode)" }
    foreach ($name in @('preview-light.png', 'preview-dark.png')) {
        if (!(Test-Path (Join-Path $stage "Assets\$name"))) { throw "Missing preview: $name" }
    }
    if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
    New-Item -ItemType Directory -Path $publish -Force | Out-Null
    Copy-Item -Path (Join-Path $stage '*') -Destination $publish -Recurse -Force
    if ($BuildOnly) {
        Write-Host "Build succeeded: $publish"
        return
    }

    Write-Host 'Reinstalling YT Music Controller for the current Windows user...'
    Assert-ArtifactPath $package
    Assert-ArtifactPath $stage
    Assert-ArtifactPath $backup
    Stop-WidgetProcesses
    $hadPackage = Test-Path -LiteralPath $package
    if ($hadPackage) { Move-Item -LiteralPath $package -Destination $backup }
    try {
        Move-Item -LiteralPath $stage -Destination $package
        Add-AppxPackage -Register (Join-Path $package 'AppxManifest.xml') -ForceApplicationShutdown
        $registered = Get-AppxPackage -Name 'Mossworm.MusicWidget'
        if (!$registered -or $registered.Status -ne 'Ok' -or $registered.InstallLocation -ne $package) {
            throw 'Package registration verification failed.'
        }
        $provider = [Activator]::CreateInstance([Type]::GetTypeFromCLSID([Guid]'4B8A0F29-125D-44D7-B27D-401486D860B4'))
        try {
            if (!$provider) { throw 'Widget provider activation failed.' }
            Write-Host 'Widget provider activation verified.'
        }
        finally {
            if ($provider -and [Runtime.InteropServices.Marshal]::IsComObject($provider)) {
                [Runtime.InteropServices.Marshal]::ReleaseComObject($provider) | Out-Null
            }
        }
    }
    catch {
        $failure = $_
        Stop-WidgetProcesses
        Assert-ArtifactPath $package
        Assert-ArtifactPath $stage
        Assert-ArtifactPath $backup
        if (Test-Path -LiteralPath $package) { Move-Item -LiteralPath $package -Destination $stage }
        if ($hadPackage) { Move-Item -LiteralPath $backup -Destination $package }
        if ($installed) {
            Add-AppxPackage -Register (Join-Path $installed.InstallLocation 'AppxManifest.xml') -ForceApplicationShutdown
        }
        throw $failure
    }
    Write-Host "Installed successfully: $($registered.PackageFullName)"
    Write-Host 'Open Win + W. If needed, add the YT Music Controller widget.'
}
finally {
    Pop-Location
    if (Test-Path -LiteralPath $build) { Remove-Item -LiteralPath $build -Recurse -Force }
}
