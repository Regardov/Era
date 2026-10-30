$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraArtifacts=Join-Path $eraRoot 'Artifacts'
$eraEditor=Join-Path $eraArtifacts 'editor-checks.json'
$eraCheckedAt=(Get-Item -LiteralPath $eraEditor).LastWriteTimeUtc
$eraSources=[ordered]@{
    accessories='Accessories027/accessories.json'
    lumen='Lumen026/lumen-runtime.json'
    lumenMotion='Lumen0261/lumen-motion.json'
    lumen60='CameraMotion-Lumen-60/camera-motion.json'
    lumen240='CameraMotion-Lumen-240/camera-motion.json'
    lumenUnlimited='CameraMotion-Lumen-0/camera-motion.json'
    archer240='CameraMotion-Ember-240/camera-motion.json'
    solo='smoke-solo.json'
    chests='smoke-chests.json'
    campaign='smoke-campaign.json'
    coopHost='smoke-host.json'
    coopClient='smoke-client.json'
    coopLumen='smoke-client2.json'
}
$eraTests=[ordered]@{}
foreach($eraName in $eraSources.Keys){
    $eraPath=Join-Path $eraArtifacts $eraSources[$eraName]
    $eraFile=Get-Item -LiteralPath $eraPath
    if($eraFile.LastWriteTimeUtc -lt $eraCheckedAt){throw "Stale Player report: $eraName"}
    $eraReport=Get-Content -LiteralPath $eraPath -Raw | ConvertFrom-Json
    if(-not $eraReport.ok){throw "Failed Player report: $eraName"}
    if($eraReport.version -and $eraReport.version -ne '0.27.0'){throw "Wrong Player version: $eraName"}
    $eraTests[$eraName]=$eraReport
}
$eraEditorReport=Get-Content -LiteralPath $eraEditor -Raw | ConvertFrom-Json
$eraLog=Get-Content (Join-Path $eraArtifacts 'unity-build.log') -Raw
if(-not $eraEditorReport.ok -or -not $eraLog.Contains('Build Finished, Result: Success.') -or -not $eraLog.Contains('ERA_BUILD_OK')){throw 'Missing successful build/editor evidence'}
$eraOutput=[ordered]@{
    ok=$true;version='0.27.0';editorAssertions=$eraEditorReport.assertions
    managedSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    gameDataSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/globalgamemanagers') -Algorithm SHA256).Hash.ToLowerInvariant()
    lumenBlendSha256=(Get-FileHash (Join-Path $eraRoot 'ArtSource/Characters/AuraWeaver/ReleaseV11/AuraWeaver_Lumen_V11.blend') -Algorithm SHA256).Hash.ToLowerInvariant()
    tests=$eraTests
    scope='Automated Windows Player tests. Camera measurements use the existing automated graphics preset and controlled routes. No physical-PC LAN, long manual balance run, or hardware FPS guarantee.'
}
$eraOutput | ConvertTo-Json -Depth 14 | Set-Content (Join-Path $eraRoot 'Docs/progression-validation-v027.json') -Encoding utf8
$eraImages=Join-Path $eraRoot 'Docs/Progression-v027'
[void](New-Item -ItemType Directory -Path $eraImages -Force)
foreach($eraImage in @('Inventory.png','Accessory-upgrade.png','Aura-upgrade.png','Hud.png','Fps-settings.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts "Accessories027/$eraImage") -Destination (Join-Path $eraImages $eraImage) -Force
}
foreach($eraImage in @('Hands-0.png','Hands-1.png','Walk-0.png','BoostRun-0.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts "Lumen0261/$eraImage") -Destination (Join-Path $eraImages $eraImage) -Force
}
Copy-Item -LiteralPath (Join-Path $eraArtifacts 'LumenHandsBefore/Idle.png') -Destination (Join-Path $eraImages 'Before-hands.png') -Force
Write-Output "PASS: $($eraTests.Count) fresh Player reports and $($eraEditorReport.assertions) editor assertions recorded."
