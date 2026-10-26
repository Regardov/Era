$ErrorActionPreference='Stop'
$eraProject=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraProject 'Artifacts/ArcherRuntime'
New-Item -ItemType Directory -Force -Path $eraFolder | Out-Null
$eraReport=Join-Path $eraFolder 'archer-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-archer','-job-worker-count','1','-screen-width','960','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'player.log')`"")
$eraProcess=Start-Process -FilePath (Join-Path $eraProject 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(65000)){throw 'Archer runtime check timed out'}
    $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    Write-Output ($eraResult | ConvertTo-Json -Depth 7)
    if(-not $eraResult.ok -or $eraProcess.ExitCode -ne 0){throw 'Archer runtime animation check failed; see Artifacts/ArcherRuntime/archer-runtime.json'}
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
