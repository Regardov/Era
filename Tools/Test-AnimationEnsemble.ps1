param([switch]$Baseline,[switch]$Record,[int]$Fps=30)
$ErrorActionPreference='Stop'
$eraProject=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraLabel=if($Baseline){'Before'}else{'After'}
$eraFolder=Join-Path $eraProject "Artifacts/AnimationQuality/Ensemble-$eraLabel-$Fps"
New-Item -ItemType Directory -Force -Path $eraFolder | Out-Null
$eraReport=Join-Path $eraFolder 'ensemble-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-ensemble','--quality-fps',"$Fps",'-job-worker-count','1','-screen-width','960','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'player.log')`"")
if($Baseline){$eraArgs+='--quality-baseline'}
if($Record){$eraArgs+='--quality-record'}
$eraProcess=Start-Process -FilePath (Join-Path $eraProject 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
 $eraProcess.PriorityClass='BelowNormal'
 if(-not $eraProcess.WaitForExit(180000)){throw 'Ensemble animation check timed out'}
 $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
 if(-not $eraResult.ok -or $eraProcess.ExitCode -ne 0){throw "Ensemble failed: errors=$($eraResult.errors); see $eraReport"}
 if(-not $Baseline){
  $eraResult.actors | Where-Object {$_.hasStrideClearance} | ForEach-Object {if($_.strideClearance -lt 1){throw "Missing garment stride response: $($_.name)"}}
  if(($eraResult.actors | Where-Object name -eq 'Arrow').arrowOffset -gt .0001){throw 'Arrow still wobbles away from its trajectory'}
  if(($eraResult.actors | Where-Object name -eq 'HiveMind').wingRange -lt 5){throw 'Drone wings remain static'}
 }
 Write-Output "PASS Ensemble $eraLabel / $Fps FPS: actors=$($eraResult.actors.Count), errors=$($eraResult.errors)"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
