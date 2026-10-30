param([switch]$Visible)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/Polish028'
[void](New-Item -ItemType Directory -Force -Path $eraFolder)
$eraArgs=@('--smoke-polish','-job-worker-count','1','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
$eraWindowStyle=if($Visible){'Normal'}else{'Hidden'}
$eraProcess=Start-Process (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle $eraWindowStyle
try{
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(80000)){throw 'Polish Player test timed out'}
    $eraReport=Get-Content (Join-Path $eraFolder 'polish.json') -Raw | ConvertFrom-Json
    $eraReport | ConvertTo-Json -Depth 5
    if($eraProcess.ExitCode -ne 0 -or -not $eraReport.ok){throw 'Native UI / golden aura test failed'}
    Write-Output "PASS Polish $($eraReport.version): actual native UI, aura range, motion, pause and cleanup"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
