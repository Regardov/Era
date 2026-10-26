param([ValidateSet(15,30,60)][int]$Fps=30)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot "Artifacts/ArcherMotion-$Fps"
New-Item -ItemType Directory -Force -Path $eraFolder | Out-Null
$eraReport=Join-Path $eraFolder 'motion-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-archer-motion','--motion-fps',"$Fps",'-job-worker-count','1','-screen-width','960','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
$eraP=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
    $eraP.PriorityClass='BelowNormal'
    if(-not $eraP.WaitForExit(90000)){throw 'Archer motion runtime test timed out'}
    $eraResult=Get-Content $eraReport -Raw | ConvertFrom-Json
    $eraResult.phases | Select-Object name,chest,shoulder,cape,maxGripError,maxQuiver,shots | Format-Table
    if(-not $eraResult.ok -or $eraP.ExitCode -ne 0){throw "Motion runtime failed at requested $Fps FPS; see $eraReport"}
    Write-Output "PASS ArcherMotion: requested=$Fps measured=$($eraResult.measuredFps); shots=$($eraResult.shots); events=$($eraResult.arrowEvents); settledCloth=$($eraResult.settledCloak)"
}finally{if(-not $eraP.HasExited){Stop-Process -Id $eraP.Id}}
