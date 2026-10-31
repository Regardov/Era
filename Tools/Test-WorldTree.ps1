$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/WorldTree'
New-Item -ItemType Directory -Force -Path $eraFolder | Out-Null
$eraReport=Join-Path $eraFolder 'world-tree.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-world-tree','-job-worker-count','2','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
$eraProcess=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
    if(-not $eraProcess.WaitForExit(180000)){throw 'Seven-realm world tree test timed out'}
    if(-not (Test-Path -LiteralPath $eraReport)){throw 'World tree Player test did not write a report'}
    $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if($eraProcess.ExitCode -ne 0 -or -not $eraResult.ok){throw ('World tree test failed: '+($eraResult | ConvertTo-Json -Depth 5 -Compress))}
    $eraResult | ConvertTo-Json -Depth 5
    Write-Output 'PASS World tree: seven centres, scale, visible sky, LOD, collision and sanctuary'
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
