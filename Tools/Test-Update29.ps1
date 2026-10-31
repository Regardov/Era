param([switch]$Visible)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/Update029'
[void](New-Item -ItemType Directory -Force -Path $eraFolder)
$eraArgs=@('--smoke-update29','-job-worker-count','1','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
$eraStyle=if($Visible){'Normal'}else{'Hidden'}
$eraProcess=Start-Process (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle $eraStyle
try{
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(80000)){throw 'Update29 Player test timed out'}
    $eraReport=Get-Content (Join-Path $eraFolder 'update29.json') -Raw | ConvertFrom-Json
    $eraReport | ConvertTo-Json -Depth 6
    if($eraProcess.ExitCode -ne 0 -or -not $eraReport.ok){throw 'Supplied icons / Echo / level-25 UI test failed; use -Visible for native UI capture'}
    Write-Output "PASS Update29 $($eraReport.version): ten transparent icons, refined Echo and native weapon choices"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
