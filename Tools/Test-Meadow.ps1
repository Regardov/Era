$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder = Join-Path $eraProject 'Artifacts'
$eraReport = Join-Path $eraFolder 'smoke-meadow.json'
if (Test-Path -LiteralPath $eraReport) { Remove-Item -LiteralPath $eraReport }
$eraArguments = @('--smoke-meadow','-job-worker-count','1','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'meadow-player.log')`"")
$eraProcess = Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArguments -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass = 'BelowNormal'
    if (-not $eraProcess.WaitForExit(45000)) { throw 'Meadow player check timed out.' }
    if (-not (Test-Path -LiteralPath $eraReport)) { throw 'Meadow check did not write its report.' }
    $eraResult = Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if (-not $eraResult.ok -or $eraProcess.ExitCode -ne 0) { throw "Meadow check failed: $($eraResult | ConvertTo-Json -Compress)" }
    Write-Output "PASS Meadow: icons=$($eraResult.suppliedIcons), renderers=$($eraResult.renderers), streams=$($eraResult.streams), grass=$($eraResult.grass), partyScale=$($eraResult.partyScale), aftermath=$($eraResult.aftermath), growth=$($eraResult.growth), reset=$($eraResult.reset)"
} finally { if (-not $eraProcess.HasExited) { Stop-Process -Id $eraProcess.Id } }
