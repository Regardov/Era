param([switch]$Record)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/AnimationQuality/Actions'
New-Item -ItemType Directory -Force -Path $eraFolder|Out-Null
$eraReport=Join-Path $eraFolder 'archer-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-archer','-job-worker-count','1','-screen-width','960','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
if($Record){$eraArgs+='--quality-record-actions'}
$eraProcess=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
 $eraProcess.PriorityClass='BelowNormal';if(-not $eraProcess.WaitForExit(90000)){throw 'Archer action review timed out'}
 $eraResult=Get-Content -LiteralPath $eraReport -Raw|ConvertFrom-Json
 if(-not $eraResult.ok -or $eraProcess.ExitCode -ne 0 -or -not $eraResult.authoredDeathEnabledByDefault){throw 'Archer action validation failed'}
 Write-Output "PASS ArcherActions: clips=$($eraResult.clips.Count), defaultDeath=$($eraResult.authoredDeathEnabledByDefault), shots=$($eraResult.shots), releases=$($eraResult.arrowEvents)"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
