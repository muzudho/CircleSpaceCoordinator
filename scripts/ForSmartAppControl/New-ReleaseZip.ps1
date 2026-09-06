[CmdletBinding()]
param(
	[string]$Configuration = 'Release',

	[string]$RuntimeIdentifier = 'win-x64',

	[string]$CertificateThumbprint,

	[string]$ZipPath
)

$ErrorActionPreference = 'Stop'
$scriptsRoot = Split-Path -Path $PSScriptRoot -Parent
$repositoryRoot = Split-Path -Path $scriptsRoot -Parent

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
	# Creates a replacement if needed and makes the current user's PC trust it.
	$certificateScript = Join-Path $PSScriptRoot 'New-DevelopmentCodeSigningCertificate.ps1'
	$certificate = & $certificateScript |
		Select-Object -Last 1
	if ($null -eq $certificate -or [string]::IsNullOrWhiteSpace($certificate.Thumbprint)) {
		throw 'Could not obtain a development code-signing certificate.'
	}
	$CertificateThumbprint = $certificate.Thumbprint
}
else {
	$certificate = Get-Item -Path "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction Stop
	if ($certificate.NotAfter -le (Get-Date)) {
		throw "The specified certificate has expired: $CertificateThumbprint"
	}
}

$publishParameters = @{
	CertificateThumbprint = $CertificateThumbprint
	Configuration = $Configuration
	RuntimeIdentifier = $RuntimeIdentifier
}
$publishScript = Join-Path $PSScriptRoot 'Publish-AndSign.ps1'
& $publishScript @publishParameters
if ($LASTEXITCODE -ne 0) {
	throw 'Failed to create the signed release artifacts.'
}

$desktopDirectory = Join-Path $repositoryRoot 'artifacts\signed\CircleSpaceCoordinator.Desktop'
if (-not (Test-Path -LiteralPath $desktopDirectory -PathType Container)) {
	throw "Desktop publish folder was not found: $desktopDirectory"
}

$invalidSignatures = @(
	Get-ChildItem -LiteralPath $desktopDirectory -Recurse -File |
		Where-Object { $_.Extension -in '.exe', '.dll' } |
		ForEach-Object { Get-AuthenticodeSignature -FilePath $_.FullName } |
		Where-Object { $_.Status -ne 'Valid' }
)
if ($invalidSignatures.Count -gt 0) {
	$details = $invalidSignatures | ForEach-Object { "$($_.Path): $($_.Status)" }
	$detailsText = $details -join [Environment]::NewLine
	throw "Some files do not have a Valid signature:`n$detailsText"
}

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
	$releaseDirectory = Join-Path $repositoryRoot 'artifacts\release'
	New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
	$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
	$ZipPath = Join-Path $releaseDirectory "CircleSpaceCoordinator.Desktop-$RuntimeIdentifier-$timestamp.zip"
}
else {
	$ZipPath = [System.IO.Path]::GetFullPath($ZipPath)
	$zipDirectory = Split-Path -Path $ZipPath -Parent
	New-Item -ItemType Directory -Path $zipDirectory -Force | Out-Null
}

& (Join-Path $PSScriptRoot 'Test-PublicReleaseContent.ps1') -Path $desktopDirectory
Compress-Archive -LiteralPath $desktopDirectory -DestinationPath $ZipPath -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
	$expectedExe = 'CircleSpaceCoordinator.Desktop/CircleSpaceCoordinator.Desktop.exe'
	$zipEntryNames = $archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
	if ($zipEntryNames -notcontains $expectedExe) {
		throw "The created ZIP does not contain the application executable: $expectedExe"
	}
}
finally {
	$archive.Dispose()
}

$signedFileCount = @(
	Get-ChildItem -LiteralPath $desktopDirectory -Recurse -File |
		Where-Object { $_.Extension -in '.exe', '.dll' }
).Count

[pscustomobject]@{
	ZipPath = $ZipPath
	CertificateThumbprint = $CertificateThumbprint
	RuntimeIdentifier = $RuntimeIdentifier
	SignedFileCount = $signedFileCount
}
