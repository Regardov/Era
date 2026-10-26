$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder = Join-Path $eraProject 'Artifacts'
$eraReport = Join-Path $eraFolder 'smoke-environment.json'
if (Test-Path -LiteralPath $eraReport) { Remove-Item -LiteralPath $eraReport }
$eraArguments = @('--smoke-environment','-job-worker-count','2','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'environment-player.log')`"")
$eraProcess = Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArguments -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass = 'BelowNormal'
    if (-not $eraProcess.WaitForExit(55000)) { throw 'Environment check timed out.' }
    if (-not (Test-Path -LiteralPath $eraReport)) { throw 'Environment check did not write its report.' }
    $eraResult = Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if (-not $eraResult.ok -or $eraProcess.ExitCode -ne 0) { throw "Environment check failed: $($eraResult | ConvertTo-Json -Compress)" }
    if (Select-String -LiteralPath (Join-Path $eraFolder 'environment-player.log') -Pattern 'Shader error|Exception|ERROR' -Quiet) { throw 'Environment player log contains an error.' }
    Write-Output "PASS Environment: transparentWater=$($eraResult.transparentWater), depth=$($eraResult.depth), foliage=$($eraResult.foliage), map=$($eraResult.map), realmChange=$($eraResult.realmChange), streams=$($eraResult.streams), renderers=$($eraResult.renderers), buildings=$($eraResult.buildings), leafPiles=$($eraResult.leafPiles), threeAuras=$($eraResult.threeAuras)"
} finally { if (-not $eraProcess.HasExited) { Stop-Process -Id $eraProcess.Id } }
