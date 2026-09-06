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

$files = @(
	Get-ChildItem -LiteralPath $Path -Recurse -File |
		Where-Object { $_.Extension -in '.exe', '.dll' } |
		Sort-Object FullName
)
if ($files.Count -eq 0) {
	throw "No EXE or DLL files were found to sign in: $Path"
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
