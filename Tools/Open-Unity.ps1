param([string]$Unity='C:\Program Files\Unity\Hub\Editor\2023.1.22f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(-not(Test-Path -LiteralPath $Unity)){throw "Unity editor not found: $Unity"}
# Opening the editor is this script's explicit purpose; keep its interactive window visible.
Start-Process -FilePath $Unity -ArgumentList @('-projectPath',('"'+$projectPath+'"'),'-executeMethod','Era.Editor.BuildTools.OpenScene')
