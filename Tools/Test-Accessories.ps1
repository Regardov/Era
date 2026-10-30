$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/Accessories027'
[void](New-Item -ItemType Directory -Path $eraFolder -Force)
$eraArgs=@('--smoke-accessories','-job-worker-count','1','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
$eraProcess=Start-Process (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(60000)){throw 'Accessory Player test timed out'}
    $eraReport=Get-Content (Join-Path $eraFolder 'accessories.json') -Raw | ConvertFrom-Json
    if($eraProcess.ExitCode -ne 0 -or -not $eraReport.ok){throw 'Accessory Player test failed'}
    Write-Output "PASS Accessories $($eraReport.version): four persistent slots, growing ranks, three auras, snapshot and uncapped FPS"
} finally {if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
