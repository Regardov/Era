param([switch]$Headless, [switch]$Graphics, [switch]$ThreeProcesses)
$ErrorActionPreference = 'Stop'
if (-not $ThreeProcesses) {
    throw 'The build already checks three-party TCP in one Unity process. This optional test launches three complete game instances; run with -ThreeProcesses only when you intend that load. Graphics remain disabled unless -Graphics is supplied.'
}
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $projectPath 'Builds\Windows\Era.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the game first with Tools\Build.ps1' }
$artifacts = Join-Path $projectPath 'Artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$hostReport = Join-Path $artifacts 'smoke-host.json'
$clientReport = Join-Path $artifacts 'smoke-client.json'
$thirdReport = Join-Path $artifacts 'smoke-client2.json'
foreach ($report in @($hostReport, $clientReport, $thirdReport)) { if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report } }
$common = @('-screen-width', '960', '-screen-height', '540', '-screen-fullscreen', '0', '-job-worker-count', '1', '--capture-folder', "`"$artifacts`"")
if (-not $Graphics -or $Headless) { $common += @('-batchmode', '-nographics') }
$hostArgs = $common + @('--smoke-host', '-logFile', "`"$(Join-Path $artifacts 'host-player.log')`"")
$clientArgs = $common + @('--smoke-client', '-logFile', "`"$(Join-Path $artifacts 'client-player.log')`"")
$hostProcess = $clientProcess = $thirdProcess = $null
try {
    $hostProcess = Start-Process -FilePath $exe -ArgumentList $hostArgs -PassThru -WindowStyle Hidden
    $hostProcess.PriorityClass = 'BelowNormal'
    Start-Sleep -Seconds 2
    $clientProcess = Start-Process -FilePath $exe -ArgumentList $clientArgs -PassThru -WindowStyle Hidden
    $clientProcess.PriorityClass = 'BelowNormal'
    Start-Sleep -Seconds 1
    $thirdArgs = $common + @('--smoke-client2', '-logFile', "`"$(Join-Path $artifacts 'client2-player.log')`"")
    $thirdProcess = Start-Process -FilePath $exe -ArgumentList $thirdArgs -PassThru -WindowStyle Hidden
    $thirdProcess.PriorityClass = 'BelowNormal'
    if (-not $hostProcess.WaitForExit(55000) -or -not $clientProcess.WaitForExit(5000) -or -not $thirdProcess.WaitForExit(5000)) { throw 'Co-op smoke test timed out' }
    foreach ($report in @($hostReport, $clientReport, $thirdReport)) {
        if (-not (Test-Path -LiteralPath $report)) { throw "Missing test report: $report" }
        $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if (-not $result.ok -or $result.players -ne 3 -or -not $result.secondWeapons -or -not $result.auras -or -not $result.accessories -or -not $result.difficulty) { throw "Co-op check failed: $(Get-Content -LiteralPath $report -Raw)" }
        Write-Output "PASS $report : players=$($result.players), ready=$($result.ready), jump=$($result.jump), chests=$($result.chests), gold=$($result.gold), music=$($result.music)"
    }
} finally {
    foreach ($process in @($hostProcess, $clientProcess, $thirdProcess)) { if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id } }
}
