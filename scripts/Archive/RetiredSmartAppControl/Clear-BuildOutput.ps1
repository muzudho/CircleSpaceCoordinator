[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$repositoryPrefix = $repositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
[xml]$solution = Get-Content -LiteralPath (Join-Path $repositoryRoot 'CircleSpaceCoordinator.slnx') -Raw
$directories = foreach ($project in $solution.SelectNodes('//Project')) {
    $projectDirectory = Split-Path ([System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $project.Path))) -Parent
    foreach ($name in @('bin', 'obj')) {
        $target = [System.IO.Path]::GetFullPath((Join-Path $projectDirectory $name))
        if (-not $target.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Build output is outside the repository: $target"
        }
        # Reject junctions/symlinks so recursive deletion stays inside the repository.
        $ancestor = $target
        while ($ancestor -and $ancestor -ne $repositoryRoot) {
            if (Test-Path -LiteralPath $ancestor) {
                if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                    throw "Build output path contains a link: $ancestor"
                }
            }
            $ancestor = Split-Path $ancestor -Parent
        }
        if (Test-Path -LiteralPath $target) {
            $links = Get-ChildItem -LiteralPath $target -Recurse -Force -Attributes ReparsePoint
            if ($links) {
                throw "Build output contains links: $target"
            }
            $target
        }
    }
}

foreach ($directory in ($directories | Sort-Object -Unique)) {
    Write-Host "Removing $directory"
    Remove-Item -LiteralPath $directory -Recurse -Force
}
