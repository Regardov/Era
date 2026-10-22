$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder = Join-Path $eraProject 'Artifacts'
$eraReport = Join-Path $eraFolder 'smoke-campaign.json'
if (Test-Path -LiteralPath $eraReport) { Remove-Item -LiteralPath $eraReport }
$eraArguments = @('--smoke-campaign','-job-worker-count','1','-screen-width','800','-screen-height','450','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$(Join-Path $eraFolder 'campaign-player.log')`"")
$eraProcess = Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArguments -PassThru -WindowStyle Hidden
try {
    $eraProcess.PriorityClass = 'BelowNormal'
    $eraProcess.ProcessorAffinity = [IntPtr]3
    if (-not $eraProcess.WaitForExit(60000)) { throw 'Single-instance campaign check timed out.' }
    if (-not (Test-Path -LiteralPath $eraReport)) { throw 'Campaign check did not write its report.' }
    $eraResult = Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if (-not $eraResult.ok -or $eraProcess.ExitCode -ne 0) { throw "Campaign check failed: $($eraResult | ConvertTo-Json -Compress)" }
    Write-Output "PASS Campaign: realms=$($eraResult.realms), bosses=$($eraResult.bosses), portals=$($eraResult.portals), jump=$($eraResult.jump), grass=$($eraResult.grass), skeletalLumen=$($eraResult.lumenModel)"
} finally { if (-not $eraProcess.HasExited) { Stop-Process -Id $eraProcess.Id } }
