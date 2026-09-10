[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
	[string]$Path,

	[Parameter(Mandatory)]
	[string]$ReportDirectory,

	[Parameter(Mandatory)]
	[ValidateSet('Enabled', 'Disabled')]
	[string]$Signing,

	[Parameter(Mandatory)]
	[ValidateSet('Succeeded', 'Blocked', 'NotRun')]
	[string]$F5Result,

	[string]$Configuration = 'Debug',

	[string]$Attempt,

	[string]$FailureDetails,

	[datetime]$Since = (Get-Date).AddMinutes(-10)
)

$ErrorActionPreference = 'Stop'

function Get-RegistryValue {
	param(
		[Parameter(Mandatory)]
		[string]$Path,

		[Parameter(Mandatory)]
		[string]$Name
	)

	try {
		return (Get-ItemPropertyValue -LiteralPath $Path -Name $Name -ErrorAction Stop)
	}
	catch {
		return $null
	}
}

New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null

$files = @(
	Get-ChildItem -LiteralPath $Path -Recurse -File |
		Where-Object { $_.Extension -in '.exe', '.dll' } |
		Sort-Object FullName |
		ForEach-Object {
			$signature = Get-AuthenticodeSignature -LiteralPath $_.FullName
			[pscustomobject]@{
				Path = $_.FullName
				SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
				SignatureStatus = $signature.Status.ToString()
				SignatureStatusMessage = $signature.StatusMessage
				SignerSubject = $signature.SignerCertificate.Subject
				SignerThumbprint = $signature.SignerCertificate.Thumbprint
			}
		}
)

$codeIntegrityEvents = @()
try {
	$codeIntegrityEvents = @(
		Get-WinEvent -FilterHashtable @{
			LogName = 'Microsoft-Windows-CodeIntegrity/Operational'
			Id = 3033, 3077
			StartTime = $Since
		} -ErrorAction Stop |
			Sort-Object TimeCreated |
			ForEach-Object {
				[pscustomobject]@{
					TimeCreated = $_.TimeCreated.ToString('o')
					Id = $_.Id
					ProviderName = $_.ProviderName
					Message = $_.Message
				}
			}
	)
}
catch {
	$codeIntegrityEvents = @([pscustomobject]@{ Error = $_.Exception.Message })
}

$visualStudioProcesses = @(
	Get-Process -Name devenv -ErrorAction SilentlyContinue |
		Select-Object Id, ProcessName, StartTime, Path
)

$report = [ordered]@{
	CollectedAt = (Get-Date).ToString('o')
	Since = $Since.ToString('o')
	Commit = (git -C $PSScriptRoot\..\.. rev-parse HEAD 2>$null)
	Experiment = [ordered]@{
		Attempt = $Attempt
		Configuration = $Configuration
		Signing = $Signing
		F5Result = $F5Result
		FailureDetails = $FailureDetails
	}
	Windows = [ordered]@{
		ProductName = Get-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name 'ProductName'
		DisplayVersion = Get-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name 'DisplayVersion'
		CurrentBuild = Get-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name 'CurrentBuild'
		UBR = Get-RegistryValue -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name 'UBR'
	}
	SmartAppControl = [ordered]@{
		VerifiedAndReputablePolicyState = Get-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' -Name 'VerifiedAndReputablePolicyState'
	}
	VisualStudioProcesses = $visualStudioProcesses
	Files = $files
	CodeIntegrityEvents = $codeIntegrityEvents
}

$reportPath = Join-Path $ReportDirectory ('diagnostic-{0}.json' -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Host "Smart App Control diagnostic saved: $reportPath"
