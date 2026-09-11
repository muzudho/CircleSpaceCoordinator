$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$fixture = Join-Path $repositoryRoot ('artifacts\signing-tests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'engines/editor') -Force | Out-Null
$owned = @('CircleSpaceCoordinator.Desktop.Windows.exe', 'engines/editor/CircleSpaceCoordinator.EditorEngine.dll')
$external = @('MonoGame.Framework.dll', 'SDL2.dll', 'StationeryUI.dll', 'CircleSpaceCoordinator.Unknown.dll', 'engines/editor/Grpc.Core.Api.dll')
foreach ($name in $owned + $external) {
    [IO.File]::WriteAllText((Join-Path $fixture $name), 'original fixture bytes')
}
$signedPaths = [System.Collections.Generic.List[string]]::new()
# Mock certificate/signature operations; no real certificate or signing required.
function Get-Item { param($Path, $ErrorAction) [pscustomobject]@{ HasPrivateKey = $true } }
function Set-AuthenticodeSignature {
    param($FilePath, $Certificate, $HashAlgorithm)
    $signedPaths.Add($FilePath)
    [IO.File]::WriteAllText($FilePath, 'signed fixture bytes')
}
function Get-AuthenticodeSignature { param($FilePath) [pscustomobject]@{ Status = 'Valid' } }
& (Join-Path $PSScriptRoot 'Sign-ReleaseArtifacts.ps1') -Path $fixture -CertificateThumbprint ('0' * 40)
if ($signedPaths.Count -ne $owned.Count) { throw 'Unexpected signing count.' }
foreach ($name in $owned) {
    if ([IO.Path]::GetFullPath((Join-Path $fixture $name)) -notin $signedPaths) { throw "Owned artifact was not signed: $name" }
}
foreach ($name in $external) {
    if ([IO.File]::ReadAllText((Join-Path $fixture $name)) -ne 'original fixture bytes') {
        throw "External artifact was modified: $name"
    }
}
Write-Output 'Passed: owned artifacts signed; external artifacts unchanged, including nested dependencies and unknown names.'
