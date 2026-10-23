$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $projectPath 'Artifacts'
$reportPath = Join-Path $artifacts 'smoke-roguelike.json'
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
$args = @('--smoke-roguelike','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$artifacts`"",'-logFile',"`"$(Join-Path $artifacts 'roguelike-player.log')`"")
$process = Start-Process -FilePath (Join-Path $projectPath 'Builds\Windows\Era.exe') -ArgumentList $args -PassThru -WindowStyle Hidden
try {
    if (-not $process.WaitForExit(45000)) { throw 'Roguelike player check timed out' }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $report.ok -or $process.ExitCode -ne 0) { throw "Roguelike check failed: $($report | ConvertTo-Json -Compress)" }
    Write-Output "PASS Roguelike: items=$($report.items), newModels=$($report.creatures), catalogue=$($report.catalogue), tooltip=$($report.tooltips), combat=$($report.combat), fps=$($report.fps)"
} finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id } }
