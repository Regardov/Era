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
    worldTree='WorldTree/world-tree.json'
    golemTree='GolemTree-v0292/golem-tree-runtime.json'
    cameraEcho='CameraMotion-Echo-0/camera-motion.json'
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
    if($eraReport.version -and $eraReport.version -ne '0.30.0'){throw "Wrong Player version: $eraName"}
    $eraTests[$eraName]=$eraReport
}
$eraEditorReport=Get-Content -LiteralPath $eraEditor -Raw | ConvertFrom-Json
$eraLog=Get-Content (Join-Path $eraArtifacts 'unity-build.log') -Raw
if(-not $eraEditorReport.ok -or -not $eraLog.Contains('Build Finished, Result: Success.') -or -not $eraLog.Contains('ERA_BUILD_OK')){throw 'Missing successful build/editor evidence'}
$eraOutput=[ordered]@{
    ok=$true;version='0.30.0';editorAssertions=$eraEditorReport.assertions
    managedSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    gameDataSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/globalgamemanagers') -Algorithm SHA256).Hash.ToLowerInvariant()
    echoBlendSha256=(Get-FileHash (Join-Path $eraRoot 'ArtSource/Characters/Echo/ReleaseV29/Echo_Refined.blend') -Algorithm SHA256).Hash.ToLowerInvariant()
    echoSource=Get-Content (Join-Path $eraRoot 'ArtSource/Characters/Echo/ReleaseV29/validation.json') -Raw | ConvertFrom-Json
    suppliedArtwork=Get-Content (Join-Path $eraRoot 'ArtSource/Items/v0.29/manifest.json') -Raw | ConvertFrom-Json
    packedContent=Get-Content (Join-Path $eraArtifacts 'player-content.json') -Raw | ConvertFrom-Json
    tests=$eraTests
    scope='Automated Windows Player tests, visible native OnGUI captures, original-versus-cutout PNG provenance, localhost three-process co-op. No physical-PC LAN, long manual balance run, full cloth collision solver, or hardware FPS guarantee.'
}
$eraInitialGolem=Join-Path $eraArtifacts 'GolemTree-v0292/first-run.json'
if((Test-Path -LiteralPath $eraInitialGolem) -and (Get-Item -LiteralPath $eraInitialGolem).LastWriteTimeUtc -ge $eraCheckedAt){
    $eraOutput.regressionNotes=[ordered]@{
        golemInitialRun=Get-Content -LiteralPath $eraInitialGolem -Raw | ConvertFrom-Json
        explanation='Initial root movement run had one blocked=false sample, with all four positions outside the solid hull. The unchanged Player passed all four on repeat. The existing probe uses a random solo seed and wall-clock movement duration; its nondeterminism is not fixed in this update.'
    }
}
$eraPackagePath=Join-Path $eraArtifacts 'package.json'
if((Test-Path -LiteralPath $eraPackagePath) -and (Get-Item -LiteralPath $eraPackagePath).LastWriteTimeUtc -ge $eraCheckedAt){
    $eraPackage=Get-Content -LiteralPath $eraPackagePath -Raw | ConvertFrom-Json
    if(-not $eraPackage.ok -or $eraPackage.version -ne $eraOutput.version -or -not $eraPackage.noAuthoringLibrary){throw 'Package validation does not match this Player'}
    $eraOutput.package=$eraPackage
}
$eraOutput | ConvertTo-Json -Depth 14 | Set-Content (Join-Path $eraRoot 'Docs/update-validation-v030.json') -Encoding utf8
$eraImages=Join-Path $eraRoot 'Docs/Update-v030'
[void](New-Item -ItemType Directory -Path $eraImages -Force)
foreach($eraImage in @('Supplied-icons.png','Echo-idle.png','Echo-walk.png','Echo-run.png','Echo-cast.png','Echo-crouch.png','Echo-jump.png','Weapons-Lumen-RU.png','Weapons-Lumen-EN.png','Weapons-Echo-RU.png','Weapons-Ember-RU.png','Weapons-Online-RU.png','Weapons-Online-EN.png','Choose-fifth.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts "Update029/$eraImage") -Destination (Join-Path $eraImages $eraImage) -Force
}
foreach($eraImage in @('character-Echo-v016.png','characters-crouching-v017.png','grip-LightningStaff-v019.png','grip-LightningStaff-side-v019.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts $eraImage) -Destination (Join-Path $eraImages $eraImage) -Force
}
foreach($eraPair in @(@('WorldTree/World-1-gameplay.png','World-tree.png'),@('WorldTree/World-1-look-up.png','World-tree-look-up.png'),@('GolemTree-v0292/Root-block-1.png','Root-block.png'),@('GolemTree-v0292/Golem-windup.png','Golem-windup.png'),@('GolemTree-v0292/Golem-contact.png','Golem-contact.png'))){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts $eraPair[0]) -Destination (Join-Path $eraImages $eraPair[1]) -Force
}
Write-Output "PASS: $($eraTests.Count) fresh Player reports and $($eraEditorReport.assertions) editor assertions recorded."
