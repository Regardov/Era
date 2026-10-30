$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $eraRoot
try {
    $eraTests=[ordered]@{
        editor='Artifacts/editor-checks.json'
        lumen='Artifacts/Lumen026/lumen-runtime.json'
        characters='Artifacts/smoke-characters.json'
        rig='Artifacts/smoke-rig.json'
        lumen150='Artifacts/CameraMotion-Lumen-150/camera-motion.json'
        archer150='Artifacts/CameraMotion-Ember-150/camera-motion.json'
        archerRefinement='Artifacts/ArcherRefinement-150/refinement-runtime.json'
        campaign='Artifacts/smoke-campaign.json'
        items='Artifacts/ItemArt/item-art-runtime.json'
        coopHost='Artifacts/smoke-host.json'
        coopClient='Artifacts/smoke-client.json'
        coopClient2='Artifacts/smoke-client2.json'
    }
    $eraResults=[ordered]@{}
    foreach($eraPair in $eraTests.GetEnumerator()) {
        $eraResult=Get-Content -LiteralPath $eraPair.Value -Raw | ConvertFrom-Json
        if(-not $eraResult.ok){throw "Failed release evidence: $($eraPair.Value)"}
        if($eraResult.version -and $eraResult.version -ne '0.26.0'){throw "Wrong Player version in $($eraPair.Value)"}
        # Editor assertions run before Unity compiles/copies the Player assembly.
        if($eraPair.Key -ne 'editor' -and (Get-Item -LiteralPath $eraPair.Value).LastWriteTimeUtc -lt (Get-Item -LiteralPath 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll').LastWriteTimeUtc){throw "Stale check predates current assembly: $($eraPair.Value)"}
        $eraResults[$eraPair.Key]=$eraResult
    }
    $eraFolder='Docs/Lumen-v026';New-Item -ItemType Directory -Force $eraFolder | Out-Null
    foreach($eraPair in $eraTests.GetEnumerator()){Copy-Item -LiteralPath $eraPair.Value -Destination "$eraFolder/$($eraPair.Key).json" -Force}
    Copy-Item -LiteralPath 'Artifacts/Lumen026/lumen-runtime.json' -Destination 'Docs/lumen-runtime-v026.json' -Force
    foreach($eraPose in @('Idle','Walk','Run','Cast','AuraChannel','Death')){Copy-Item -LiteralPath "Artifacts/Lumen026/Lumen-$eraPose.png" -Destination "$eraFolder/Runtime-$eraPose.png" -Force}
    foreach($eraView in @('Front','Side','Back')){Copy-Item -LiteralPath "ArtSource/Characters/AuraWeaver/ReleaseV9/Previews/$eraView.png" -Destination "$eraFolder/Blender-$eraView.png" -Force}
    Copy-Item -LiteralPath 'Artifacts/character-Lumen-v016.png' -Destination "$eraFolder/Character-studio.png" -Force
    $eraReport=[ordered]@{
        version='0.26.0';date=(Get-Date -Format 'yyyy-MM-dd');unity='2023.1.22f1'
        baseCommit='5d2d45ce93da0068df2c5ce814c734ef2f9fb51a';authorAccount='ZXCJORA'
        editorAssertions=$eraResults.editor.assertions;windowsBuild='Success'
        managedSha256=(Get-FileHash 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll' -Algorithm SHA256).Hash.ToLowerInvariant()
        gameDataSha256=(Get-FileHash 'Builds/Windows/Era_Data/globalgamemanagers' -Algorithm SHA256).Hash.ToLowerInvariant()
        tests=$eraResults
        limits=@('Local pouch/cloth contacts remain during Death.','No cloth physics or full humanoid retarget certification.','Co-op tested on localhost, not WAN packet loss.','Requested 150 FPS is not a performance guarantee for every PC.','Runtime pose probe supplies PlayerState events; campaign and camera probes use ordinary simulation/input.')
    }
    $eraReport | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath 'Docs/release-validation-v026.json' -Encoding utf8
    Write-Output "Collected $($eraTests.Count) successful checks for Windows v0.26.0"
} finally {Pop-Location}
