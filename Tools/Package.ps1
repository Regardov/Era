$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildPath = Join-Path $projectPath 'Builds\Windows'
if (-not (Test-Path -LiteralPath (Join-Path $buildPath 'Era.exe'))) { throw 'Build with Tools\Build.ps1 first' }
Copy-Item -LiteralPath (Join-Path $projectPath 'Docs\PLAY-RU.md') -Destination (Join-Path $buildPath 'PLAY-RU.md') -Force
Copy-Item -LiteralPath (Join-Path $projectPath 'Docs\DOWNLOAD-RU.md') -Destination (Join-Path $buildPath 'DOWNLOAD-RU.md') -Force
if (Test-Path -LiteralPath (Join-Path $projectPath 'Docs\THIRD-PARTY.md')) {
    Copy-Item -LiteralPath (Join-Path $projectPath 'Docs\THIRD-PARTY.md') -Destination (Join-Path $buildPath 'THIRD-PARTY.md') -Force
    $licenseFolder = Join-Path $buildPath 'Licenses'
    New-Item -ItemType Directory -Force -Path $licenseFolder | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectPath 'Assets\Era\Art\KayKit') -Filter 'LICENSE-*.txt' | Copy-Item -Destination $licenseFolder -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'Releases') | Out-Null
$archive = Join-Path $projectPath 'Releases\Era-Windows.zip'
Compress-Archive -Path (Join-Path $buildPath '*') -DestinationPath $archive -Force
$eraHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($archive + '.sha256', "$eraHash  Era-Windows.zip`n", [Text.UTF8Encoding]::new($false))
Write-Output "Ready to share: $archive"
