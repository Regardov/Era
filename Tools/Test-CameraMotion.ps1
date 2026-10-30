param(
    [ValidateRange(0,1000)][int]$Fps=150,
    [ValidateSet('Ember','Echo','Lumen')][string]$Hero='Ember',
    [switch]$Baseline,
    [string]$OutputFolder=''
)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(-not $OutputFolder){$OutputFolder=Join-Path $eraRoot "Artifacts/CameraMotion-$Hero-$Fps"}
New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
$eraCameraReport=Join-Path $OutputFolder 'camera-motion.json'
if(Test-Path -LiteralPath $eraCameraReport){Remove-Item -LiteralPath $eraCameraReport}
$eraCameraArgs=@('--smoke-camera-motion','--camera-fps',"$Fps",'--camera-hero',$Hero,'-job-worker-count','1','-screen-width','960','-screen-height','540','-screen-fullscreen','0','--capture-folder',"`"$OutputFolder`"",'-logFile',"`"$OutputFolder/player.log`"")
if($Baseline){$eraCameraArgs+='--camera-baseline'}
$eraCameraPlayer=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraCameraArgs -PassThru -WindowStyle Hidden
try{
    $eraCameraPlayer.PriorityClass='BelowNormal'
    if(-not $eraCameraPlayer.WaitForExit(90000)){throw 'Camera motion test timed out'}
    $eraCameraResult=Get-Content -LiteralPath $eraCameraReport -Raw | ConvertFrom-Json
    $eraCameraResult.phases | Select-Object direction,frames,measuredFps,actorStalledFrames,actorSpeedErrorRms,healthBarStalledFrames,healthBarSpeedErrorRms,rawStalledFrames | Format-Table
    if($eraCameraPlayer.ExitCode -ne 0 -or (-not $Baseline -and -not $eraCameraResult.ok)){throw "Camera motion regression failed; see $eraCameraReport"}
    Write-Output "CameraMotion: hero=$Hero fps=$Fps baseline=$Baseline smooth=$($eraCameraResult.ok) version=$($eraCameraResult.version)"
}finally{if(-not $eraCameraPlayer.HasExited){Stop-Process -Id $eraCameraPlayer.Id}}
