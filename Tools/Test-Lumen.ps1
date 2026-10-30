$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/Lumen026'
New-Item -ItemType Directory -Force -Path $eraFolder | Out-Null
$eraArgs=@('--smoke-lumen','-job-worker-count','1','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
$eraPlayer=Start-Process (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try {
    $eraPlayer.PriorityClass='BelowNormal'
    if(-not $eraPlayer.WaitForExit(60000)){throw 'Lumen runtime validation timed out'}
    $eraReport=Get-Content (Join-Path $eraFolder 'lumen-runtime.json') -Raw | ConvertFrom-Json
    $eraReport.animations | Format-Table animation,exists,assigned,runtimePlayed,finite,boneMotionDegrees
    if(-not $eraReport.ok -or $eraPlayer.ExitCode -ne 0){throw 'Lumen runtime validation failed'}
    Write-Output "PASS Lumen $($eraReport.version): live actor, 11 clips, valid Generic Avatar"
} finally {if(-not $eraPlayer.HasExited){Stop-Process -Id $eraPlayer.Id}}
