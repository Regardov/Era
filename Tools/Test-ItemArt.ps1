$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot 'Artifacts/ItemArt'
New-Item -ItemType Directory -Path $eraFolder -Force | Out-Null
$eraReport=Join-Path $eraFolder 'item-art-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-item-art','-job-worker-count','1','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'player.log')`"")
$eraProcess=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
    if(-not $eraProcess.WaitForExit(50000)){throw 'Item artwork and chest animation check timed out'}
    $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if(-not $eraResult.ok -or $eraProcess.ExitCode -ne 0){throw "Item art check failed: $($eraResult | ConvertTo-Json -Compress)"}
    Write-Output "PASS ItemArt: icons=$($eraResult.icons), active=$($eraResult.activeIcons), alpha=$($eraResult.transparent), distinct=$($eraResult.distinct), launch=$($eraResult.launches), colors=$($eraResult.colors), styles=$($eraResult.styles), collection=$($eraResult.collection), errors=$($eraResult.errors)"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
