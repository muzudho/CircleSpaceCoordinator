$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$fixture = Join-Path $repositoryRoot ('artifacts\privacy-tests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
[System.IO.File]::WriteAllText((Join-Path $fixture 'CircleSpaceCoordinator.Desktop.exe'), 'inventory-test-only')
$gate = Join-Path $PSScriptRoot 'Test-PublicReleaseContent.ps1'
& $gate -Path $fixture | Out-Null
$passed = 1
foreach ($name in @('application-settings.json', 'private-input.xlsx', 'debug.pdb', 'unknown.json')) {
    $file = Join-Path $fixture $name
    [System.IO.File]::WriteAllText($file, 'fictional-test-content')
    $wasRejected = $false
    try { & $gate -Path $fixture | Out-Null }
    catch { $wasRejected = $true }
    # Remove only the exact fixture file created by this test.
    Remove-Item -LiteralPath $file
    if (-not $wasRejected) { throw "Release gate failed to reject test fixture: $name" }
    $passed++
}
$exampleDirectory = Join-Path $fixture 'examples'
New-Item -ItemType Directory -Path $exampleDirectory | Out-Null
$example = Join-Path $exampleDirectory 'circle-space-project-v1.example.json'
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'examples/circle-space-project-v1.example.json') -Destination $example
& $gate -Path $fixture | Out-Null
$passed++
[System.IO.File]::WriteAllText($example, '{"fictional_private_marker":true}')
$wasRejected = $false
try { & $gate -Path $fixture | Out-Null }
catch { $wasRejected = $true }
if (-not $wasRejected) { throw 'Release gate accepted a modified example.' }
$passed++
Write-Output "$passed/$passed release privacy checks passed."
