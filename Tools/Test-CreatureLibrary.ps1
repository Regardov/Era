$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/Creatures-v030-runtime'
New-Item -ItemType Directory -Force -Path $eraFolder | Out-Null
$eraReport=Join-Path $eraFolder 'creature-library-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-creature-library','-job-worker-count','2','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',('"'+$eraFolder+'"'),'-logFile',('"'+(Join-Path $eraFolder 'player.log')+'"'))
$eraP=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
 if(-not $eraP.WaitForExit(100000)){throw 'Creature runtime check timed out'}
 $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
 $eraResult | ConvertTo-Json -Depth 6
 if($eraP.ExitCode -ne 0 -or -not $eraResult.ok){throw 'Creature locomotion/combat failed'}
 Write-Output 'PASS: two golem walks/attacks and blood bat flight/dive'
}finally{if(-not $eraP.HasExited){Stop-Process -Id $eraP.Id}}
