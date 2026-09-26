[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot ('artifacts\publish\' + [Guid]::NewGuid().ToString('N'))
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Publish output must be a new directory: $OutputRoot"
}

$projects = @(
    @{ Name = 'CircleSpaceCoordinator.Desktop.Windows'; Path = 'CircleSpaceCoordinator.Desktop.Windows\CircleSpaceCoordinator.Desktop.Windows.csproj' },
    @{ Name = 'CircleSpaceCoordinator.EditorCli'; Path = 'tools\CircleSpaceCoordinator.EditorCli\CircleSpaceCoordinator.EditorCli.csproj' },
    @{ Name = 'CircleSpaceCoordinator.ProjectCli'; Path = 'tools\CircleSpaceCoordinator.ProjectCli\CircleSpaceCoordinator.ProjectCli.csproj' }
)
foreach ($project in $projects) {
    $projectPath = Join-Path $repositoryRoot $project.Path
    $publishPath = Join-Path $OutputRoot $project.Name
    $arguments = @('publish', $projectPath, '--configuration', $Configuration, '--runtime', $RuntimeIdentifier, '--output', $publishPath)
    if ($project.Name -eq 'CircleSpaceCoordinator.Desktop.Windows') {
        $arguments += @('--self-contained', 'true', '-p:DebugType=None', '-p:DebugSymbols=false')
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed: $projectPath (exit code $LASTEXITCODE)"
    }
}

[pscustomobject]@{
    OutputRoot = $OutputRoot
    DesktopDirectory = Join-Path $OutputRoot 'CircleSpaceCoordinator.Desktop.Windows'
}
