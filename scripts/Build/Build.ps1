[CmdletBinding()]
param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$buildArguments = @('build', (Join-Path $repositoryRoot 'CircleSpaceCoordinator.slnx'), '--configuration', $Configuration)
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (exit code $LASTEXITCODE)."
}
