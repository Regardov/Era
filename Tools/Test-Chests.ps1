$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder = Join-Path $eraProject 'Artifacts'
New-Item -ItemType Directory -Path $eraFolder -Force | Out-Null
$eraReport = Join-Path $eraFolder 'smoke-chests.json'
if (Test-Path -LiteralPath $eraReport) { Remove-Item -LiteralPath $eraReport }
$eraArguments = @('--smoke-chests','-job-worker-count','1','-screen-width','800','-screen-height','450','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'chests-player.log')`"")
$eraProcess = Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArguments -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass = 'BelowNormal'
    $eraProcess.ProcessorAffinity = [IntPtr]3
    if (-not $eraProcess.WaitForExit(60000)) { throw 'Single-instance chest check timed out.' }
    if (-not (Test-Path -LiteralPath $eraReport)) { throw 'Chest check did not write its report.' }
    $eraResult = Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if (-not $eraResult.ok -or $eraProcess.ExitCode -ne 0) { throw "Chest check failed: $($eraResult | ConvertTo-Json -Compress)" }
    Write-Output "PASS Chests: first=$($eraResult.firstPrice), nextMoon=$($eraResult.nextMoonPrice), models=$($eraResult.models), animation=$($eraResult.opened), auraUpgrades=$($eraResult.upgrades)"
} finally { if (-not $eraProcess.HasExited) { Stop-Process -Id $eraProcess.Id } }
