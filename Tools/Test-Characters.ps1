$ErrorActionPreference='Stop'
$eraProject=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraProject 'Artifacts'
$eraReport=Join-Path $eraFolder 'smoke-characters.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-characters','-job-worker-count','1','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'characters-player.log')`"")
$eraProcess=Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(45000)){throw 'Character player check timed out'}
    $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if(-not $eraResult.ok -or $eraProcess.ExitCode -ne 0){throw "Character check failed: $($eraResult | ConvertTo-Json -Compress)"}
    Write-Output "PASS Characters: models=$($eraResult.models), skins=$($eraResult.skins), clips=$($eraResult.clips), crouch=$($eraResult.crouch)"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
