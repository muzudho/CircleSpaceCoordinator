$ErrorActionPreference = 'Stop'
# Exercise the actual ZIP signature filter without publishing or using a certificate.
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'New-ReleaseZip.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Release script syntax error.' }
$filter = $ast.Find({
    param($node)
    $node -is [System.Management.Automation.Language.CommandAst] -and
    $node.GetCommandName() -eq 'Where-Object' -and $node.Extent.Text.Contains('$owned =')
}, $true)
if ($null -eq $filter) { throw 'ZIP signature filter not found.' }
$predicate = $filter.CommandElements[1].ScriptBlock.GetScriptBlock()
$cases = @(
    @{ File = 'CircleSpaceCoordinator.Desktop.Windows.exe'; Status = 'Valid'; Rejected = $false },
    @{ File = 'engines/editor/CircleSpaceCoordinator.EditorEngine.dll'; Status = 'NotSigned'; Rejected = $true },
    @{ File = 'CircleSpaceCoordinator.Core.dll'; Status = 'HashMismatch'; Rejected = $true },
    @{ File = 'StationeryUI.dll'; Status = 'NotSigned'; Rejected = $false },
    @{ File = 'SDL2.dll'; Status = 'Valid'; Rejected = $false },
    @{ File = 'engines/editor/Grpc.Core.Api.dll'; Status = 'HashMismatch'; Rejected = $true },
    @{ File = 'external.dll'; Status = 'NotTrusted'; Rejected = $true }
)
foreach ($case in $cases) {
    $signature = [pscustomobject]@{ Path = $case.File; Status = $case.Status }
    $rejected = @($signature | Where-Object $predicate).Count -gt 0
    if ($rejected -ne $case.Rejected) { throw "Incorrect signature decision: $($case.File), $($case.Status)" }
}
Write-Output '7/7 ZIP signature policy checks passed.'
