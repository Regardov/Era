$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $projectPath 'Artifacts'
$arguments = @('--smoke-art','-screen-width','1600','-screen-height','900','-screen-fullscreen','0','--capture-folder',"`"$artifacts`"",'-logFile',"`"$(Join-Path $artifacts 'art-player.log')`"")
$process = Start-Process -FilePath (Join-Path $projectPath 'Builds\Windows\Era.exe') -ArgumentList $arguments -PassThru -WindowStyle Hidden
try {
    if (-not $process.WaitForExit(45000)) { throw 'Art and shrine smoke test timed out' }
    $report = Get-Content -LiteralPath (Join-Path $artifacts 'smoke-art.json') -Raw | ConvertFrom-Json
    if (-not $report.ok -or $process.ExitCode -ne 0) { throw "Art check failed: $($report | ConvertTo-Json -Compress)" }
    Write-Output "PASS Models=$($report.models), shrine=$($report.randomBuff), crouch=$($report.crouch), extraJump=$($report.extraJump), descriptions=$($report.descriptions), crystals=$($report.blueCrystals)"
} finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id } }
