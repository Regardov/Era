param([int]$Width=800,[int]$Height=450)
$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder = Join-Path $eraProject 'Artifacts'
$eraReport = Join-Path $eraFolder 'smoke-update.json'
if (Test-Path -LiteralPath $eraReport) { Remove-Item -LiteralPath $eraReport }
$eraArguments = @('--smoke-update','-job-worker-count','1','-screen-width',$Width.ToString(),'-screen-height',$Height.ToString(),'-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'update-player.log')`"")
$eraProcess = Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArguments -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass = 'BelowNormal'
    $eraProcess.ProcessorAffinity = [IntPtr]3
    $eraDeadline = (Get-Date).AddSeconds(95)
    while (-not $eraProcess.WaitForExit(1000)) { if ((Get-Date) -gt $eraDeadline) { throw 'Update check timed out.' } }
    if (-not (Test-Path -LiteralPath $eraReport)) { throw 'Update check did not write its report.' }
    $eraResult = Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if (-not $eraResult.ok -or $eraProcess.ExitCode -ne 0) { throw "Update check failed: $($eraResult | ConvertTo-Json -Compress)" }
    Write-Output "PASS Update: waves=$($eraResult.waves), statue=$($eraResult.statue), elites=$($eraResult.guards), freeChest=$($eraResult.freeChest), resources=$($eraResult.nodes), slide=$($eraResult.slide), humans=$($eraResult.humans), music=$($eraResult.music)"
} finally { if (-not $eraProcess.HasExited) { Stop-Process -Id $eraProcess.Id } }
