$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/Lumen0261'
New-Item -ItemType Directory -Force $eraFolder | Out-Null
$eraArgs=@('--smoke-lumen-motion','-job-worker-count','1','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
$eraProcess=Start-Process (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(90000)){throw 'Lumen gait/rune/robe test timed out'}
    $eraReport=Get-Content "$eraFolder/lumen-motion.json" -Raw | ConvertFrom-Json
    $eraReport.phases | Format-Table name,frames,maxPenetration,maxCorrection,maxLean,maxWalkWeight,maxRunWeight,rune
    if(-not $eraReport.ok -or $eraProcess.ExitCode -ne 0){throw 'Lumen gait/rune/robe validation failed'}
    Write-Output "PASS Lumen actual movement, rune and garment clearance $($eraReport.version)"
} finally {if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
