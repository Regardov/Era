param([string]$Version='0.26.1',[string]$Suffix='0261',[string]$BaseCommit='d66491a58d3aa6a6bfec8da363c9967f0030fa28')
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $eraRoot
try {
    $eraTests=[ordered]@{
        editor='Artifacts/editor-checks.json'
        lumen='Artifacts/Lumen026/lumen-runtime.json'
        lumenMotion='Artifacts/Lumen0261/lumen-motion.json'
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
        if($eraResult.version -and $eraResult.version -ne $Version){throw "Wrong Player version in $($eraPair.Value)"}
        # Editor assertions run before Unity compiles/copies the Player assembly.
        if($eraPair.Key -ne 'editor' -and (Get-Item -LiteralPath $eraPair.Value).LastWriteTimeUtc -lt (Get-Item -LiteralPath 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll').LastWriteTimeUtc){throw "Stale check predates current assembly: $($eraPair.Value)"}
        $eraResults[$eraPair.Key]=$eraResult
    }
    $eraFolder="Docs/Lumen-v$Suffix";New-Item -ItemType Directory -Force $eraFolder | Out-Null
    foreach($eraPair in $eraTests.GetEnumerator()){Copy-Item -LiteralPath $eraPair.Value -Destination "$eraFolder/$($eraPair.Key).json" -Force}
    Copy-Item -LiteralPath 'Artifacts/Lumen026/lumen-runtime.json' -Destination "Docs/lumen-runtime-v$Suffix.json" -Force
    Get-ChildItem 'Artifacts/Lumen0261' -Filter '*.png' | Copy-Item -Destination $eraFolder -Force
    foreach($eraPose in @('Idle','Walk','Run','Cast','AuraChannel','Death')){Copy-Item -LiteralPath "Artifacts/Lumen026/Lumen-$eraPose.png" -Destination "$eraFolder/Runtime-$eraPose.png" -Force}
    foreach($eraView in @('Side','Back')){Copy-Item -LiteralPath "ArtSource/Characters/AuraWeaver/ReleaseV10/Previews/$eraView.png" -Destination "$eraFolder/Blender-$eraView.png" -Force}
    Copy-Item -LiteralPath 'Artifacts/character-Lumen-v016.png' -Destination "$eraFolder/Character-studio.png" -Force
    $eraReport=[ordered]@{
        version=$Version;date=(Get-Date -Format 'yyyy-MM-dd');unity='2023.1.22f1'
        baseCommit=$BaseCommit;authorAccount='ZXCJORA'
        editorAssertions=$eraResults.editor.assertions;windowsBuild='Success'
        managedSha256=(Get-FileHash 'Builds/Windows/Era_Data/Managed/Assembly-CSharp.dll' -Algorithm SHA256).Hash.ToLowerInvariant()
        gameDataSha256=(Get-FileHash 'Builds/Windows/Era_Data/globalgamemanagers' -Algorithm SHA256).Hash.ToLowerInvariant()
        tests=$eraResults
        limits=@('Garment correction uses leg capsules, not full cloth physics.','No full humanoid retarget certification.','Co-op tested on localhost, not WAN packet loss.','Requested 150 FPS is not a performance guarantee for every PC.','Runtime clip probe supplies PlayerState events; gait/rune/garment, campaign and camera probes use ordinary simulation/input.')
    }
    if(Test-Path -LiteralPath 'Artifacts/Release0261/coop-first-host.json') {
        $eraReport['observations']=@{coopFirstRun='Initial unchanged three-process smoke did not confirm blessing on any peer; repeat passed. Cause not established; no shrine gameplay change was made.'}
        foreach($eraPeer in @('host','client','client2')){Copy-Item -LiteralPath "Artifacts/Release0261/coop-first-$eraPeer.json" -Destination "$eraFolder/coop-first-$eraPeer.json" -Force}
    }
    $eraReport | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath "Docs/release-validation-v$Suffix.json" -Encoding utf8
    Write-Output "Collected $($eraTests.Count) successful checks for Windows v$Version"
} finally {Pop-Location}
