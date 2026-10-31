$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraArtifacts=Join-Path $eraRoot 'Artifacts'
$eraEditor=Join-Path $eraArtifacts 'editor-checks.json'
$eraCheckedAt=(Get-Item -LiteralPath $eraEditor).LastWriteTimeUtc
$eraSources=[ordered]@{
    update29='Update029/update29.json'
    itemArt='ItemArt/item-art-runtime.json'
    characters='smoke-characters.json'
    rig='smoke-rig.json'
    animation='smoke-animation.json'
    polish='Polish028/polish.json'
    accessories='Accessories027/accessories.json'
    solo='smoke-solo.json'
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
    if($eraReport.version -and $eraReport.version -ne '0.29.0'){throw "Wrong Player version: $eraName"}
    $eraTests[$eraName]=$eraReport
}
$eraEditorReport=Get-Content -LiteralPath $eraEditor -Raw | ConvertFrom-Json
$eraLog=Get-Content (Join-Path $eraArtifacts 'unity-build.log') -Raw
if(-not $eraEditorReport.ok -or -not $eraLog.Contains('Build Finished, Result: Success.') -or -not $eraLog.Contains('ERA_BUILD_OK')){throw 'Missing successful build/editor evidence'}
$eraOutput=[ordered]@{
    ok=$true;version='0.29.0';editorAssertions=$eraEditorReport.assertions
    managedSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    gameDataSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/globalgamemanagers') -Algorithm SHA256).Hash.ToLowerInvariant()
    echoBlendSha256=(Get-FileHash (Join-Path $eraRoot 'ArtSource/Characters/Echo/ReleaseV29/Echo_Refined.blend') -Algorithm SHA256).Hash.ToLowerInvariant()
    echoSource=Get-Content (Join-Path $eraRoot 'ArtSource/Characters/Echo/ReleaseV29/validation.json') -Raw | ConvertFrom-Json
    suppliedArtwork=Get-Content (Join-Path $eraRoot 'ArtSource/Items/v0.29/manifest.json') -Raw | ConvertFrom-Json
    tests=$eraTests
    scope='Automated Windows Player tests, visible native OnGUI captures, original-versus-cutout PNG provenance, localhost three-process co-op. No physical-PC LAN, long manual balance run, full cloth collision solver, or hardware FPS guarantee.'
}
$eraOutput | ConvertTo-Json -Depth 14 | Set-Content (Join-Path $eraRoot 'Docs/update-validation-v029.json') -Encoding utf8
$eraImages=Join-Path $eraRoot 'Docs/Update-v029'
[void](New-Item -ItemType Directory -Path $eraImages -Force)
foreach($eraImage in @('Supplied-icons.png','Echo-idle.png','Echo-walk.png','Echo-run.png','Echo-cast.png','Echo-crouch.png','Echo-jump.png','Weapons-Lumen-RU.png','Weapons-Lumen-EN.png','Weapons-Echo-RU.png','Weapons-Ember-RU.png','Weapons-Online-RU.png','Weapons-Online-EN.png','Choose-fifth.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts "Update029/$eraImage") -Destination (Join-Path $eraImages $eraImage) -Force
}
foreach($eraImage in @('character-Echo-v016.png','characters-crouching-v017.png','grip-LightningStaff-v019.png','grip-LightningStaff-side-v019.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts $eraImage) -Destination (Join-Path $eraImages $eraImage) -Force
}
Write-Output "PASS: $($eraTests.Count) fresh Player reports and $($eraEditorReport.assertions) editor assertions recorded."
