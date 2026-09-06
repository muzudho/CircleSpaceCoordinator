[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'
$releaseRoot = (Resolve-Path -LiteralPath $Path).Path.TrimEnd('\', '/')
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$rejected = @()
$allowedJson = @('CircleSpaceCoordinator.Desktop.deps.json', 'CircleSpaceCoordinator.Desktop.runtimeconfig.json')
if (Get-ChildItem -LiteralPath $releaseRoot -Recurse -Force -Directory |
    Where-Object { $_.Attributes -band [System.IO.FileAttributes]::ReparsePoint }) {
    throw 'Public release folders must not contain filesystem links.'
}
foreach ($file in Get-ChildItem -LiteralPath $releaseRoot -Recurse -Force -File) {
    $relative = $file.FullName.Substring($releaseRoot.Length + 1).Replace('\', '/')
    # Accept only runtime binaries and the two explicitly shipped assets.
    # This is a packaging check, not an audit of compiled or signed metadata.
    if ($relative -match '(^|/)(logs|projects|private|\.git)(/|$)' -or
        ($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        $rejected += $relative
        continue
    }
    if ($file.Extension -in '.dll', '.exe') { continue }
    if ($relative -in $allowedJson) { continue }
    if ($relative -eq 'Assets/long-table-app-icon.png') { continue }
    if ($relative -eq 'examples/circle-space-project-v1.example.json') {
        $expected = Join-Path $repositoryRoot 'examples/circle-space-project-v1.example.json'
        if ((Get-FileHash -LiteralPath $file.FullName).Hash -eq (Get-FileHash -LiteralPath $expected).Hash) { continue }
    }
    $rejected += $relative
}
if ($rejected.Count -gt 0) {
    # Do not print filenames: they may themselves contain private event names.
    throw "Public release content check rejected $($rejected.Count) unexpected file(s). Inspect the publish folder locally; do not distribute it."
}
if (-not (Test-Path -LiteralPath (Join-Path $releaseRoot 'CircleSpaceCoordinator.Desktop.exe') -PathType Leaf)) {
    throw 'Public release content check: the application executable is missing.'
}
Write-Output 'Public release file inventory check passed. Binary metadata and source/history review are separate checks.'
