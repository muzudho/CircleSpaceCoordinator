[CmdletBinding()]
param(
	[string]$Subject = 'CN=CircleSpaceCoordinator Development Code Signing',
	[int]$ValidYears = 3
)

$existingCertificate = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert |
	Where-Object { $_.Subject -eq $Subject } |
	Sort-Object NotAfter -Descending |
	Select-Object -First 1

if ($null -ne $existingCertificate -and $existingCertificate.NotAfter -gt (Get-Date)) {
	$certificate = $existingCertificate
	Write-Host "既存の開発用コードサイニング証明書を使用します: $($certificate.Thumbprint)"
}
else {
	$certificate = New-SelfSignedCertificate `
		-Type CodeSigningCert `
		-Subject $Subject `
		-CertStoreLocation Cert:\CurrentUser\My `
		-KeyAlgorithm RSA `
		-KeyLength 3072 `
		-HashAlgorithm SHA256 `
		-NotAfter (Get-Date).AddYears($ValidYears)

	Write-Host "開発用コードサイニング証明書を作成しました: $($certificate.Thumbprint)"
}

$certificateFile = Join-Path $env:TEMP "$($certificate.Thumbprint).cer"
Export-Certificate -Cert $certificate -FilePath $certificateFile -Force | Out-Null

try {
	foreach ($store in 'Cert:\CurrentUser\Root', 'Cert:\CurrentUser\TrustedPublisher') {
		$alreadyTrusted = Get-ChildItem -Path $store |
			Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }

		if ($null -eq $alreadyTrusted) {
			Import-Certificate -FilePath $certificateFile -CertStoreLocation $store | Out-Null
			Write-Host "証明書を信頼ストアへ追加しました: $store"
		}
	}
}
finally {
	Remove-Item -Path $certificateFile -Force -ErrorAction SilentlyContinue
}

[pscustomobject]@{
	Subject = $certificate.Subject
	Thumbprint = $certificate.Thumbprint
	NotAfter = $certificate.NotAfter
}
