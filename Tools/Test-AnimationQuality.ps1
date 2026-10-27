param([ValidateSet(15,30,60)][int]$Fps=30,[switch]$Record,[switch]$Baseline)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraLabel=if($Baseline){'Before'}else{'After'}
$eraFolder=Join-Path $eraRoot "Artifacts/AnimationQuality/$eraLabel-$Fps"
New-Item -ItemType Directory -Force -Path $eraFolder|Out-Null
$eraReport=Join-Path $eraFolder 'refinement-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-archer-refinement','--master-quality','--refinement-fps',"$Fps",'-job-worker-count','1','-screen-width','960','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
if($Record){$eraArgs+='--refinement-record'}
$eraProcess=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(150000)){throw 'Animation quality runtime timed out'}
    $eraResult=Get-Content -LiteralPath $eraReport -Raw|ConvertFrom-Json
    if(-not $eraResult.ok -or $eraProcess.ExitCode -ne 0){throw "Quality runtime failed: $($eraResult.failures -join '; ')"}
    foreach($eraName in @('RunCast','RunCastShoot')){
        $eraPhase=$eraResult.phases|Where-Object name -EQ $eraName
        if(-not $Baseline -and ($eraPhase.castFrames -lt 2 -or $eraPhase.castWithLocomotionFrames -lt $eraPhase.castFrames*.8)){throw "Cast replaces locomotion: $eraName"}
        Write-Output "$eraName castFrames=$($eraPhase.castFrames) movingLegFrames=$($eraPhase.castWithLocomotionFrames)"
    }
    Write-Output "PASS Quality $eraLabel / $Fps FPS; shots=$($eraResult.shots), releases=$($eraResult.events)"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
