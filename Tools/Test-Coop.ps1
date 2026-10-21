param([switch]$Headless)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $projectPath 'Builds\Windows\Era.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the game first with Tools\Build.ps1' }
$artifacts = Join-Path $projectPath 'Artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$hostReport = Join-Path $artifacts 'smoke-host.json'
$clientReport = Join-Path $artifacts 'smoke-client.json'
foreach ($report in @($hostReport, $clientReport)) { if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report } }
$common = @('-screen-width', '1600', '-screen-height', '900', '-screen-fullscreen', '0', '--capture-folder', "`"$artifacts`"")
if ($Headless) { $common += @('-batchmode', '-nographics') }
$hostArgs = $common + @('--smoke-host', '-logFile', "`"$(Join-Path $artifacts 'host-player.log')`"")
$clientArgs = $common + @('--smoke-client', '-logFile', "`"$(Join-Path $artifacts 'client-player.log')`"")
$hostProcess = Start-Process -FilePath $exe -ArgumentList $hostArgs -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
$clientProcess = Start-Process -FilePath $exe -ArgumentList $clientArgs -PassThru -WindowStyle Hidden
try {
    if (-not $hostProcess.WaitForExit(60000) -or -not $clientProcess.WaitForExit(5000)) { throw 'Co-op smoke test timed out' }
    foreach ($report in @($hostReport, $clientReport)) {
        if (-not (Test-Path -LiteralPath $report)) { throw "Missing test report: $report" }
        $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if (-not $result.ok) { throw "Co-op check failed: $(Get-Content -LiteralPath $report -Raw)" }
        Write-Output "PASS $report : players=$($result.players), ready=$($result.ready), jump=$($result.jump), chests=$($result.chests), gold=$($result.gold), music=$($result.music)"
    }
} finally {
    foreach ($process in @($hostProcess, $clientProcess)) { if (-not $process.HasExited) { Stop-Process -Id $process.Id } }
}
