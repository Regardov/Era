$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $projectPath 'Builds\Windows\Era.exe'
$artifacts = Join-Path $projectPath 'Artifacts'
$arguments = @('--smoke-solo','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$artifacts`"",'-logFile',"`"$(Join-Path $artifacts 'solo-player.log')`"")
$process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
try {
    if (-not $process.WaitForExit(45000)) { throw 'Solo smoke test timed out' }
    $report = Get-Content -LiteralPath (Join-Path $artifacts 'smoke-solo.json') -Raw | ConvertFrom-Json
    if (-not $report.ok -or $process.ExitCode -ne 0) { throw "Solo check failed: $($report | ConvertTo-Json -Compress)" }
    Write-Output "PASS Solo: paused=$($report.paused), resumed=$($report.resumed), attributes=$($report.attributeRolls), rarityLoot=$($report.rarityLoot), items=$($report.itemCount), weapons=$($report.weaponCount)"
} finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id } }
