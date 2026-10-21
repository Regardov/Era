param([string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2023.1.22f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
# Compile a source snapshot so an open editor and unsaved scene edits need not be closed.
$snapshotPath = Join-Path $projectPath 'Artifacts\Validation-v03'
New-Item -ItemType Directory -Force -Path $snapshotPath | Out-Null
foreach ($directory in @('Assets','Packages','ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $projectPath $directory) -Destination $snapshotPath -Recurse -Force
}
$log = Join-Path $projectPath 'Artifacts\unity-build-v03.log'
$output = Join-Path $projectPath 'Builds\Windows'
$arguments = @('-batchmode','-nographics','-quit','-projectPath',"`"$snapshotPath`"",'-executeMethod','Era.Editor.BuildTools.BuildWindows','--era-output',"`"$output`"",'-logFile',"`"$log`"")
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
$process.WaitForExit()
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $log -Tail 75; throw "Isolated Unity build failed ($($process.ExitCode))" }
# Bring the generated original icon assets and GUIDs back into the editable project.
$icons = Join-Path $snapshotPath 'Assets\Era\Resources\Icons'
Copy-Item -LiteralPath $icons -Destination (Join-Path $projectPath 'Assets\Era\Resources') -Recurse -Force
Copy-Item -LiteralPath ($icons + '.meta') -Destination (Join-Path $projectPath 'Assets\Era\Resources\Icons.meta') -Force
Copy-Item -LiteralPath (Join-Path $snapshotPath 'Artifacts\editor-checks.json') -Destination (Join-Path $projectPath 'Artifacts\editor-checks.json') -Force
foreach ($assetFolder in @('Art\KayKit','Resources\Textures','Resources\Characters','Resources\Creatures')) {
    $generated = Join-Path $snapshotPath "Assets\Era\$assetFolder"
    $destinationParent = Split-Path (Join-Path $projectPath "Assets\Era\$assetFolder")
    if (Test-Path -LiteralPath $generated) { Copy-Item -LiteralPath $generated -Destination $destinationParent -Recurse -Force }
}
Write-Output "Build ready: $output\Era.exe"
