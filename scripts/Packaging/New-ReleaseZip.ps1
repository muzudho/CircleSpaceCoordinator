[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$ZipPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$publishRoot = Join-Path $repositoryRoot ('artifacts\release\publish-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $PSScriptRoot 'Publish.ps1') -Configuration $Configuration -RuntimeIdentifier $RuntimeIdentifier -OutputRoot $publishRoot | Out-Null
$desktopDirectory = Join-Path $publishRoot 'CircleSpaceCoordinator.Desktop.Windows'
& (Join-Path $PSScriptRoot 'Test-PublicReleaseContent.ps1') -Path $desktopDirectory

$desktopExe = Join-Path $desktopDirectory 'CircleSpaceCoordinator.Desktop.Windows.exe'
$releaseVersion = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($desktopExe).ProductVersion -split '\+', 2)[0]
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Unexpected release version in published executable: $releaseVersion"
}

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $releaseDirectory = Join-Path $repositoryRoot 'artifacts\release'
    New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $ZipPath = Join-Path $releaseDirectory "CircleSpaceCoordinator.Desktop.Windows-v$releaseVersion-$RuntimeIdentifier-$timestamp.zip"
}
else {
    $ZipPath = [System.IO.Path]::GetFullPath($ZipPath)
    New-Item -ItemType Directory -Path (Split-Path $ZipPath -Parent) -Force | Out-Null
}
if (Test-Path -LiteralPath $ZipPath) {
    throw "ZIP already exists: $ZipPath"
}
Compress-Archive -LiteralPath $desktopDirectory -DestinationPath $ZipPath -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
    $expectedExe = 'CircleSpaceCoordinator.Desktop.Windows/CircleSpaceCoordinator.Desktop.Windows.exe'
    $entryNames = $archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
    if ($entryNames -notcontains $expectedExe) {
        throw "The ZIP does not contain the application executable: $expectedExe"
    }
}
finally {
    $archive.Dispose()
}

[pscustomobject]@{
    Version = $releaseVersion
    ZipPath = $ZipPath
    RuntimeIdentifier = $RuntimeIdentifier
    PublishRoot = $publishRoot
}
