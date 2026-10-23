$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildPath = Join-Path $projectPath 'Builds\Windows'
if (-not (Test-Path -LiteralPath (Join-Path $buildPath 'Era.exe'))) { throw 'Build with Tools\Build.ps1 first' }
# Only remove outdated documents from this generated build folder.
foreach ($eraOldDocument in @('ITEMS-v0.12.md','RELEASE-v0.12.0.md','VALIDATION-v0.12.md')) {
    $eraOldPath = Join-Path $buildPath $eraOldDocument
    if (Test-Path -LiteralPath $eraOldPath) { Remove-Item -LiteralPath $eraOldPath }
}
Copy-Item -LiteralPath (Join-Path $projectPath 'Docs\PLAY-RU.md') -Destination (Join-Path $buildPath 'PLAY-RU.md') -Force
Copy-Item -LiteralPath (Join-Path $projectPath 'Docs\DOWNLOAD-RU.md') -Destination (Join-Path $buildPath 'DOWNLOAD-RU.md') -Force
foreach ($eraDocument in @('ITEMS-v0.13.md','RELEASE-v0.13.0.md','VALIDATION-v0.13.md')) {
    $eraDocumentPath = Join-Path $projectPath "Docs\$eraDocument"
    if (Test-Path -LiteralPath $eraDocumentPath) { Copy-Item -LiteralPath $eraDocumentPath -Destination (Join-Path $buildPath $eraDocument) -Force }
}
$eraImageFolder = Join-Path $buildPath 'Images'
New-Item -ItemType Directory -Force -Path $eraImageFolder | Out-Null
foreach ($eraImage in @('balance-menu-catalogue-v013.png','balance-item-tooltip-v013.png','balance-models-standing-v013.png','balance-models-crouching-v013.png','balance-stat-tooltip-v013.png','balance-shield-evade-v013.png')) {
    Copy-Item -LiteralPath (Join-Path $projectPath "Docs\Images\$eraImage") -Destination (Join-Path $eraImageFolder $eraImage) -Force
}
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
