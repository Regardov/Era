$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildPath = Join-Path $projectPath 'Builds\Windows'
if (-not (Test-Path -LiteralPath (Join-Path $buildPath 'Era.exe'))) { throw 'Build with Tools\Build.ps1 first' }
# Only remove outdated documents from this generated build folder.
foreach ($eraOldDocument in @('ITEMS-v0.12.md','RELEASE-v0.12.0.md','VALIDATION-v0.12.md','ITEMS-v0.13.md','RELEASE-v0.13.0.md','VALIDATION-v0.13.md','RELEASE-v0.14.0.md','VALIDATION-v0.14.md','RELEASE-v0.15.0.md','VALIDATION-v0.15.md','RELEASE-v0.16.0.md','VALIDATION-v0.16.md','RELEASE-v0.17.0.md','VALIDATION-v0.17.md','RELEASE-v0.18.0.md','VALIDATION-v0.18.md')) {
    $eraOldPath = Join-Path $buildPath $eraOldDocument
    if (Test-Path -LiteralPath $eraOldPath) { Remove-Item -LiteralPath $eraOldPath }
}
Copy-Item -LiteralPath (Join-Path $projectPath 'Docs\PLAY-RU.md') -Destination (Join-Path $buildPath 'PLAY-RU.md') -Force
Copy-Item -LiteralPath (Join-Path $projectPath 'Docs\DOWNLOAD-RU.md') -Destination (Join-Path $buildPath 'DOWNLOAD-RU.md') -Force
foreach ($eraDocument in @('ITEMS-v0.14.md','RELEASE-v0.20.0.md','FEEDBACK-VALIDATION.md','MECHANIC-FEEDBACK-AUDIT.md','feedback-changed-files.txt','ENVIRONMENT-POLISH.md','MUSIC.md','CHARACTER-BODY-v0.19.md','ANIMATION-AUDIT-v0.18.md','body-checks-v019.json','rig-smoke-v019.json','animation-smoke-v019.json')) {
    $eraDocumentPath = Join-Path $projectPath "Docs\$eraDocument"
    if (Test-Path -LiteralPath $eraDocumentPath) { Copy-Item -LiteralPath $eraDocumentPath -Destination (Join-Path $buildPath $eraDocument) -Force }
}
$eraImageFolder = Join-Path $buildPath 'Images'
New-Item -ItemType Directory -Force -Path $eraImageFolder | Out-Null
Get-ChildItem -LiteralPath $eraImageFolder -File -Filter '*.png' | Where-Object { $_.Name -match 'v01[23]\.png$' } | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
foreach ($eraImage in @('balance-menu-catalogue-v014.png','balance-item-tooltip-v014.png','balance-models-standing-v014.png','balance-models-crouching-v014.png','balance-stat-tooltip-v014.png','balance-shield-evade-v014.png','balance-boss-health-v014.png')) {
    Copy-Item -LiteralPath (Join-Path $projectPath "Docs\Images\$eraImage") -Destination (Join-Path $eraImageFolder $eraImage) -Force
}
foreach ($eraImage in @('living-meadow-v015.png','raised-boss-hud-v015.png','boss-aftermath-v015.png','new-icons-1-v015.png','new-icons-2-v015.png','new-icons-3-v015.png','new-icons-4-v015.png','character-Ember-v016.png','character-Lumen-v016.png','character-Echo-v016.png','character-Stalker-v016.png','character-Wisp-v016.png','characters-running-v016.png','characters-crouching-v016.png')) {
    Copy-Item -LiteralPath (Join-Path $projectPath "Docs\Images\$eraImage") -Destination (Join-Path $eraImageFolder $eraImage) -Force
}
foreach ($eraHero in @('ember','echo','lumen')) {
    Copy-Item -LiteralPath (Join-Path $projectPath "Docs\Images\hero-$eraHero-v016.png") -Destination (Join-Path $eraImageFolder "hero-$eraHero-v016.png") -Force
}
Get-ChildItem -LiteralPath (Join-Path $projectPath 'Docs\Images') -File -Filter '*-v019.png' | Copy-Item -Destination $eraImageFolder -Force
Get-ChildItem -LiteralPath (Join-Path $projectPath 'Docs\Images') -File -Filter '*-v021.png' | Copy-Item -Destination $eraImageFolder -Force
Get-ChildItem -LiteralPath (Join-Path $projectPath 'Docs') -File -Filter 'feedback-*.json' | Copy-Item -Destination $buildPath -Force
foreach ($eraImage in @('character-Echo-v017.png','characters-crouching-v017.png','characters-crouching-side-v017.png')) {
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
& (Join-Path $PSScriptRoot 'Verify-Package.ps1')
