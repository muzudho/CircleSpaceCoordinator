[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [ValidateSet('true', 'false')]
    [string]$SmartAppControlSigningEnabled
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
& (Join-Path $PSScriptRoot 'Clear-BuildOutput.ps1')

# Run outside MSBuild: obj must be removed before NuGet restore and project evaluation.
$buildArguments = @('build', (Join-Path $repositoryRoot 'CircleSpaceCoordinator.slnx'), '--configuration', $Configuration)
if ($SmartAppControlSigningEnabled) {
    $buildArguments += "-p:SmartAppControlSigningEnabled=$SmartAppControlSigningEnabled"
}
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Clean build failed (exit code $LASTEXITCODE)."
}
