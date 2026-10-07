[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Configuration = "Release",

    [ValidateNotNullOrEmpty()]
    [string]$OutputPath = "artifacts\packages",

    [string]$VersionSuffix,

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$solutionPath = Join-Path $PSScriptRoot "Microsoft.AI.Local.slnx"
$packageOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
}
else {
    Join-Path $PSScriptRoot $OutputPath
}

$packageOutputPath = [System.IO.Path]::GetFullPath($packageOutputPath)
New-Item -ItemType Directory -Path $packageOutputPath -Force | Out-Null

$packArguments = @(
    "pack"
    $solutionPath
    "--configuration"
    $Configuration
    "--output"
    $packageOutputPath
)

if ($VersionSuffix) {
    $packArguments += @("--version-suffix", $VersionSuffix)
}

if ($NoRestore) {
    $packArguments += "--no-restore"
}

& dotnet @packArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$packages = Get-ChildItem -Path $packageOutputPath -File |
    Where-Object { $_.Extension -in ".nupkg", ".snupkg" } |
    Sort-Object Name

Write-Host ""
Write-Host "Local NuGet feed: $packageOutputPath"
foreach ($package in $packages) {
    Write-Host "  $($package.Name)"
}

Write-Host ""
Write-Host "To test a package in another app:"
Write-Host "  dotnet add package <PackageId> --source `"$packageOutputPath`" --prerelease"
