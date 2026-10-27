param(
 [string]$Blender='C:/Program Files/Blender Foundation/Blender 5.2/blender.exe',
 [string]$Unity='C:/Program Files/Unity/Hub/Editor/2023.1.22f1/Editor/Unity.exe',
 [switch]$SkipUnity,
 [switch]$SkipBlender
)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFactory=Join-Path $eraRoot 'ArtSource/CharacterFactory'
$eraOut=Join-Path $eraRoot 'Artifacts/CharacterFactory'
New-Item -ItemType Directory -Force -Path $eraOut | Out-Null
$eraSource=Join-Path $eraRoot 'ArtSource/Characters/Archer/Archer_Animated.blend'
$eraBefore=(Get-FileHash -LiteralPath $eraSource -Algorithm SHA256).Hash
foreach($eraScript in $(if($SkipBlender){@()}else{@('build_library.py','validate_factory.py')})){
 $eraLog=Join-Path $eraOut ($eraScript+'.log')
 & $Blender --background --factory-startup --python-exit-code 2 --python (Join-Path $eraFactory $eraScript) *> $eraLog
 if($LASTEXITCODE -ne 0){throw "Factory Blender step failed: $eraLog"}
}
if((Get-FileHash -LiteralPath $eraSource -Algorithm SHA256).Hash -ne $eraBefore){throw 'Reference changed'}
if(-not $SkipUnity){
 $eraProbe=Join-Path $eraOut 'UnityProbe'
 if((Resolve-Path $eraOut).Path -notlike "$eraRoot*"){throw 'Unexpected test target'}
 foreach($eraDir in @('Assets/Editor','Assets/OriginalClips','Packages','ProjectSettings')){New-Item -ItemType Directory -Force -Path (Join-Path $eraProbe $eraDir) | Out-Null}
 Copy-Item -LiteralPath (Join-Path $eraOut 'FactorySmoke.fbx') -Destination (Join-Path $eraProbe 'Assets/FactorySmoke.fbx')
 Copy-Item -LiteralPath (Join-Path $eraFactory 'Validation/UnityFactoryProbe.cs') -Destination (Join-Path $eraProbe 'Assets/Editor/UnityFactoryProbe.cs')
 Copy-Item -LiteralPath (Join-Path $eraRoot 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $eraProbe 'ProjectSettings/ProjectVersion.txt')
 '{"dependencies":{"com.unity.modules.animation":"1.0.0","com.unity.modules.jsonserialize":"1.0.0"}}' | Set-Content -LiteralPath (Join-Path $eraProbe 'Packages/manifest.json') -Encoding UTF8
 $eraArt=Join-Path $eraRoot 'Assets/Era/Art/Characters/Archer'
 Get-ChildItem -LiteralPath (Join-Path $eraArt 'Animations') -File | Where-Object {$_.Name -like '*.anim' -or $_.Name -like '*.anim.meta'} | Copy-Item -Destination (Join-Path $eraProbe 'Assets/OriginalClips')
 foreach($eraName in @('Archer.controller','Archer.controller.meta','Archer_BodyActions.mask','Archer_BodyActions.mask.meta','Archer_UpperBody.mask','Archer_UpperBody.mask.meta')){Copy-Item -LiteralPath (Join-Path $eraArt $eraName) -Destination (Join-Path $eraProbe 'Assets/OriginalClips')}
 $eraLog=Join-Path $eraOut 'unity-probe.log'
 $eraProcess=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-nographics','-quit','-job-worker-count','2','-projectPath',"`"$eraProbe`"",'-executeMethod','EraFactory.UnityFactoryProbe.Run','-logFile',"`"$eraLog`"") -PassThru -WindowStyle Hidden
 try{if(-not $eraProcess.WaitForExit(240000)){throw "Unity probe timed out: $eraLog"};if($eraProcess.ExitCode -ne 0){throw "Unity probe failed: $eraLog"}}
 finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
 $eraReport=Get-Content -LiteralPath (Join-Path $eraOut 'unity-validation.json') -Raw | ConvertFrom-Json
 if(-not $eraReport.ok){throw 'Unity report failed'}
 Copy-Item -LiteralPath (Join-Path $eraOut 'unity-validation.json') -Destination (Join-Path $eraFactory 'Measurements/unity-validation.json')
 Write-Output "PASS Unity factory: $($eraReport.clips.Count) original clips; Generic Avatar; $($eraReport.bones) bones"
}
Write-Output "PASS Factory: reference SHA256 preserved; library and independent smoke copy validated"
