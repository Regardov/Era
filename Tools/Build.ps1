param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2023.1.22f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity 2023.1.22f1 not found: $Unity" }
New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'Artifacts') | Out-Null
$buildLog = Join-Path $projectPath 'Artifacts\unity-build.log'
$unityArgs = @('-batchmode', '-nographics', '-quit', '-job-worker-count', '2', '-projectPath', "`"$projectPath`"", '-executeMethod', 'Era.Editor.BuildTools.BuildWindows', '-logFile', "`"$buildLog`"")
$eraBuildStarted = Get-Date
$process = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru -WindowStyle Hidden
try { $process.PriorityClass = 'BelowNormal' } catch { Write-Warning 'Could not lower Unity process priority.' }
try {
    [long]$eraAllowedAffinity = $process.ProcessorAffinity.ToInt64()
    [long]$eraBuildAffinity = 0
    $eraBuildCores = 0
    for ($eraCore = 0; $eraCore -lt 63 -and $eraBuildCores -lt 2; $eraCore++) {
        [long]$eraBit = [long]1 -shl $eraCore
        if (($eraAllowedAffinity -band $eraBit) -ne 0) { $eraBuildAffinity = $eraBuildAffinity -bor $eraBit; $eraBuildCores++ }
    }
    if ($eraBuildAffinity -ne 0) { $process.ProcessorAffinity = [IntPtr]$eraBuildAffinity }
} catch { Write-Warning 'Could not restrict Unity to two CPU cores.' }
$eraShutdownHandled = $false
while (-not $process.WaitForExit(1000)) {
    # Some Unity installations hang after successfully saving the player and closing licensing.
    # Only stop our process after the current log confirms both build completion and shutdown.
    if ((Test-Path -LiteralPath $buildLog) -and (Get-Item -LiteralPath $buildLog).LastWriteTime -ge $eraBuildStarted -and ((Get-Date) - (Get-Item -LiteralPath $buildLog).LastWriteTime).TotalSeconds -gt 10) {
        $eraLogTail = Get-Content -LiteralPath $buildLog -Tail 60 | Out-String
        if ($eraLogTail.Contains('ERA_BUILD_OK') -and $eraLogTail.Contains('Batchmode quit successfully invoked')) {
            $process.Kill()
            if (-not $process.WaitForExit(5000)) {
                $eraRemainingBuild = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
                if ($eraRemainingBuild -and -not $eraRemainingBuild.HasExited) { throw 'Player build succeeded, but the Unity background process did not stop.' }
            }
            $eraShutdownHandled = $true
            Write-Output 'Build succeeded; closed Unity after its shutdown stalled.'
            break
        }
    }
}
if (-not $eraShutdownHandled -and $process.ExitCode -ne 0) {
    $eraCompletedLog = Get-Content -LiteralPath $buildLog -Raw
    $eraAssembly = Join-Path $projectPath 'Builds\Windows\Era_Data\Managed\Assembly-CSharp.dll'
    $eraSavedBuild = $eraCompletedLog.Contains('ERA_CHECKS_OK') -and $eraCompletedLog.Contains('ERA_BUILD_OK') -and $eraCompletedLog.Contains('Build Finished, Result: Success.') -and (Test-Path -LiteralPath $eraAssembly) -and (Get-Item -LiteralPath $eraAssembly).LastWriteTime -ge $eraBuildStarted
    if ($eraSavedBuild -and $eraCompletedLog.Contains('UnityEngine::Analytics::CloudJobScheduler::ScheduleJob')) {
        Write-Warning 'Player and checks saved successfully; Unity reported an Analytics shutdown failure after completion. Validate the saved player with Test-Campaign.ps1.'
    } else { Get-Content -LiteralPath $buildLog -Tail 90; throw "Unity build failed ($($process.ExitCode))" }
}
Write-Output "Build ready: $(Join-Path $projectPath 'Builds\Windows\Era.exe')"
