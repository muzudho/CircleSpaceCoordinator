[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidatePattern('^[A-Fa-f0-9]{40}$')]
	[string]$CertificateThumbprint,

	[string]$Configuration = 'Release',

	[string]$RuntimeIdentifier,

	[string]$TimestampServer,

	[string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$scriptsRoot = Split-Path -Path $PSScriptRoot -Parent
$repositoryRoot = Split-Path -Path $scriptsRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
	$OutputRoot = Join-Path $repositoryRoot 'artifacts\signed'
}
$projects = @(
	@{ Name = 'CircleSpaceCoordinator.Desktop.Windows'; Path = 'CircleSpaceCoordinator.Desktop.Windows\CircleSpaceCoordinator.Desktop.Windows.csproj' },
	@{ Name = 'CircleSpaceCoordinator.EditorCli'; Path = 'tools\CircleSpaceCoordinator.EditorCli\CircleSpaceCoordinator.EditorCli.csproj' },
	@{ Name = 'CircleSpaceCoordinator.ProjectCli'; Path = 'tools\CircleSpaceCoordinator.ProjectCli\CircleSpaceCoordinator.ProjectCli.csproj' }
)

& (Join-Path $scriptsRoot 'Build\Clear-BuildOutput.ps1')

foreach ($project in $projects) {
	$projectPath = Join-Path $repositoryRoot $project.Path
	$publishPath = Join-Path $OutputRoot $project.Name
	$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
	$resolvedPublishPath = [System.IO.Path]::GetFullPath($publishPath)
	if (-not $resolvedPublishPath.StartsWith($resolvedOutputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
		throw "安全でない publish 出力先を拒否しました: $resolvedPublishPath"
	}
	if (Test-Path -LiteralPath $resolvedPublishPath -PathType Container) {
		Remove-Item -LiteralPath $resolvedPublishPath -Recurse -Force
	}
	$publishArguments = @('publish', $projectPath, '--configuration', $Configuration, '--output', $publishPath)

	if ($RuntimeIdentifier) {
		$publishArguments += @('--runtime', $RuntimeIdentifier)
	}
	if ($project.Name -eq 'CircleSpaceCoordinator.Desktop.Windows' -and $RuntimeIdentifier) {
		$publishArguments += @(
			'--self-contained', 'true',
			'-p:DebugType=None',
			'-p:DebugSymbols=false'
		)
	}

	& dotnet @publishArguments
	if ($LASTEXITCODE -ne 0) {
		throw "publish に失敗しました: $projectPath"
	}

	$signArguments = @{
		Path = $publishPath
		CertificateThumbprint = $CertificateThumbprint
	}
	if ($TimestampServer) {
		$signArguments.TimestampServer = $TimestampServer
	}

	& (Join-Path $PSScriptRoot 'Sign-ReleaseArtifacts.ps1') @signArguments
}
