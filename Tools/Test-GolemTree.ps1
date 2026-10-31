$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/GolemTree-v0292'
New-Item -ItemType Directory -Path $eraFolder -Force | Out-Null
$eraReport=Join-Path $eraFolder 'golem-tree-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-golem-tree','-job-worker-count','2','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','--capture-folder',('"'+$eraFolder+'"'),'-logFile',('"'+(Join-Path $eraFolder 'player.log')+'"'))
$eraP=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try {
 if(-not $eraP.WaitForExit(90000)){throw 'Golem/tree runtime check timed out'}
 if(-not (Test-Path -LiteralPath $eraReport)){throw 'No runtime report'}
 $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
 $eraResult | ConvertTo-Json -Depth 6
 if($eraP.ExitCode -ne 0 -or -not $eraResult.ok){throw 'Golem/tree runtime regression failed'}
 Write-Output 'PASS: actual root movement block, new golem, windup, contact, damage and recovery'
} finally {if(-not $eraP.HasExited){Stop-Process -Id $eraP.Id}}
