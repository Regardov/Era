$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder = Join-Path $eraProject 'Artifacts'
$eraReport = Join-Path $eraFolder 'smoke-music.json'
if (Test-Path -LiteralPath $eraReport) { Remove-Item -LiteralPath $eraReport }
$eraArguments = @('--smoke-music','-job-worker-count','2','-screen-width','800','-screen-height','450','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'music-player.log')`"")
$eraProcess = Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArguments -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass = 'BelowNormal'
    if (-not $eraProcess.WaitForExit(110000)) { throw 'Music check timed out.' }
    if (-not (Test-Path -LiteralPath $eraReport)) { throw 'Music check did not write its report.' }
    $eraResult = Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if (-not $eraResult.ok -or $eraProcess.ExitCode -ne 0) { throw "Music check failed: $($eraResult | ConvertTo-Json -Compress)" }
    if (Select-String -LiteralPath (Join-Path $eraFolder 'music-player.log') -Pattern 'Exception|ERROR|Failed to load audio' -Quiet) { throw 'Music player log contains an error.' }
    Write-Output "PASS Music: menu=$($eraResult.menu), realms=$($eraResult.realmTracks), streaming=$($eraResult.streaming), transitions=$($eraResult.transitions), pause=$($eraResult.pause), mute=$($eraResult.mute), loop=$($eraResult.loop), return=$($eraResult.returnToMenu), rapid=$($eraResult.rapidChanges), sources=$($eraResult.sources)"
} finally { if (-not $eraProcess.HasExited) { Stop-Process -Id $eraProcess.Id } }
