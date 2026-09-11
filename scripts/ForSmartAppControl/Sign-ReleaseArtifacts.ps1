[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidateScript({ Test-Path -Path $_ -PathType Container })]
	[string]$Path,

	[Parameter(Mandatory)]
	[ValidatePattern('^[A-Fa-f0-9]{40}$')]
	[string]$CertificateThumbprint,

	[string]$TimestampServer
)

$certificate = Get-Item -Path "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction Stop
if (-not $certificate.HasPrivateKey) {
	throw "Certificate '$CertificateThumbprint' does not have a private key."
}

# Explicit allowlist of assemblies built from this repository. Never re-sign
# third-party dependencies, including StationeryUI, MonoGame and native DLLs.
$ownedAssemblies = @(
	'CircleSpaceCoordinator.Application',
	'CircleSpaceCoordinator.Calculations',
	'CircleSpaceCoordinator.Core',
	'CircleSpaceCoordinator.Desktop.Core',
	'CircleSpaceCoordinator.Desktop.Windows',
	'CircleSpaceCoordinator.EditorClient',
	'CircleSpaceCoordinator.EditorCli',
	'CircleSpaceCoordinator.EditorEngine',
	'CircleSpaceCoordinator.Engine.Contracts',
	'CircleSpaceCoordinator.Engine.Model',
	'CircleSpaceCoordinator.Infrastructure',
	'CircleSpaceCoordinator.OptimizationEngine',
	'CircleSpaceCoordinator.ProjectCli',
	'CircleSpaceCoordinator.StationeryUI',
	'CircleSpaceCoordinator.TableIO',
	'CircleSpaceCoordinator.ThinkingEngine'
)
$files = @(
	Get-ChildItem -LiteralPath $Path -Recurse -File |
		Where-Object { $_.Extension -in '.exe', '.dll' -and $_.BaseName -in $ownedAssemblies } |
		Sort-Object FullName
)
if ($files.Count -eq 0) {
	throw "No repository-owned EXE or DLL files were found to sign in: $Path"
}

foreach ($file in $files) {
	$parameters = @{
		FilePath = $file.FullName
		Certificate = $certificate
		HashAlgorithm = 'SHA256'
	}

	if ($TimestampServer) {
		$parameters.TimestampServer = $TimestampServer
	}

	Set-AuthenticodeSignature @parameters | Out-Null

	$signature = Get-AuthenticodeSignature -FilePath $file.FullName
	if ($signature.Status -ne 'Valid') {
		throw "Signature validation failed: $($file.FullName) ($($signature.Status): $($signature.StatusMessage))"
	}

	Write-Host "Signed and validated: $($file.FullName)"
}

Write-Host "Signed and validated $($files.Count) files."
