$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraArtifacts = Join-Path $eraProject 'Artifacts'
$eraReport = Join-Path $eraArtifacts 'smoke-balance.json'
if (Test-Path -LiteralPath $eraReport) { Remove-Item -LiteralPath $eraReport }
$eraArgs = @('--smoke-balance','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$eraArtifacts`"",'-logFile',"`"$(Join-Path $eraArtifacts 'balance-player.log')`"")
$eraProcess = Start-Process -FilePath (Join-Path $eraProject 'Builds\Windows\Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try {
    if (-not $eraProcess.WaitForExit(45000)) { throw 'Balance player check timed out' }
    $eraResult = Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    if (-not $eraResult.ok -or $eraProcess.ExitCode -ne 0) { throw "Balance check failed: $($eraResult | ConvertTo-Json -Compress)" }
    Write-Output "PASS Balance: menuCatalogue=$($eraResult.menuCatalogue), crouch=$($eraResult.crouch), shield=$($eraResult.shield), evade=$($eraResult.evade), tooltip=$($eraResult.tooltip), chests=$($eraResult.chests)"
} finally { if (-not $eraProcess.HasExited) { Stop-Process -Id $eraProcess.Id } }
