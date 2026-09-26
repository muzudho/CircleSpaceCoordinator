[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
	[string]$Path,

	[string]$CertificateSubject = 'CN=CircleSpaceCoordinator Development Code Signing'
)

$ErrorActionPreference = 'Stop'

$certificate = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert |
	Where-Object {
		$_.Subject -eq $CertificateSubject -and
		$_.HasPrivateKey -and
		$_.NotBefore -le (Get-Date) -and
		$_.NotAfter -gt (Get-Date)
	} |
	Sort-Object NotAfter -Descending |
	Select-Object -First 1

if ($null -eq $certificate) {
	throw "A valid code-signing certificate was not found for '$CertificateSubject'. Run New-DevelopmentCodeSigningCertificate.ps1 first."
}

& (Join-Path $PSScriptRoot 'Sign-ReleaseArtifacts.ps1') `
	-Path $Path `
	-CertificateThumbprint $certificate.Thumbprint
