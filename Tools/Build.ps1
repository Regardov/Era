param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2023.1.22f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity 2023.1.22f1 not found: $Unity" }
New-Item -ItemType Directory -Force -Path (Join-Path $projectPath 'Artifacts') | Out-Null
$buildLog = Join-Path $projectPath 'Artifacts\unity-build.log'
$unityArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', "`"$projectPath`"", '-executeMethod', 'Era.Editor.BuildTools.BuildWindows', '-logFile', "`"$buildLog`"")
$process = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $buildLog -Tail 90; throw "Unity build failed ($($process.ExitCode))" }
Write-Output "Build ready: $(Join-Path $projectPath 'Builds\Windows\Era.exe')"
