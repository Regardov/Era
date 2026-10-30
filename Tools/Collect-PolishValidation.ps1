$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraArtifacts=Join-Path $eraRoot 'Artifacts'
$eraEditor=Join-Path $eraArtifacts 'editor-checks.json'
$eraCheckedAt=(Get-Item -LiteralPath $eraEditor).LastWriteTimeUtc
$eraSources=[ordered]@{
    polish='Polish028/polish.json'
    lumenMotion='Lumen0261/lumen-motion.json'
    lumen='Lumen026/lumen-runtime.json'
    lumenUnlimited='CameraMotion-Lumen-0/camera-motion.json'
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
    if($eraReport.version -and $eraReport.version -ne '0.28.0'){throw "Wrong Player version: $eraName"}
    $eraTests[$eraName]=$eraReport
}
$eraEditorReport=Get-Content -LiteralPath $eraEditor -Raw | ConvertFrom-Json
$eraLog=Get-Content (Join-Path $eraArtifacts 'unity-build.log') -Raw
if(-not $eraEditorReport.ok -or -not $eraLog.Contains('Build Finished, Result: Success.') -or -not $eraLog.Contains('ERA_BUILD_OK')){throw 'Missing successful build/editor evidence'}
$eraOutput=[ordered]@{
    ok=$true;version='0.28.0';editorAssertions=$eraEditorReport.assertions
    managedSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    gameDataSha256=(Get-FileHash (Join-Path $eraRoot 'Builds/Windows/Era_Data/globalgamemanagers') -Algorithm SHA256).Hash.ToLowerInvariant()
    lumenBlendSha256=(Get-FileHash (Join-Path $eraRoot 'ArtSource/Characters/AuraWeaver/ReleaseV12/AuraWeaver_Lumen_V12.blend') -Algorithm SHA256).Hash.ToLowerInvariant()
    sourceFootContacts=Get-Content (Join-Path $eraRoot 'ArtSource/Characters/AuraWeaver/ReleaseV12/contacts.json') -Raw | ConvertFrom-Json
    tests=$eraTests
    scope='Windows Player tests, including a visible native OnGUI capture and metric audit. Camera measurements use the automated graphics preset and controlled routes, with weapons and aura disabled. Localhost three-process co-op; no physical-PC LAN or hardware FPS guarantee.'
}
$eraOutput | ConvertTo-Json -Depth 14 | Set-Content (Join-Path $eraRoot 'Docs/polish-validation-v028.json') -Encoding utf8
$eraImages=Join-Path $eraRoot 'Docs/Polish-v028'
[void](New-Item -ItemType Directory -Path $eraImages -Force)
foreach($eraImage in @('Native-Russian-cards.png','Native-English-cards.png','Native-Online-cards.png','Golden-aura.png','Aura-surge.png','Aura-moving.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts "Polish028/$eraImage") -Destination (Join-Path $eraImages $eraImage) -Force
}
foreach($eraImage in @('Walk-0.png','Walk-1.png','BoostRun-0.png')){
    Copy-Item -LiteralPath (Join-Path $eraArtifacts "Lumen0261/$eraImage") -Destination (Join-Path $eraImages $eraImage) -Force
}
Write-Output "PASS: $($eraTests.Count) fresh Player reports and $($eraEditorReport.assertions) editor assertions recorded."
