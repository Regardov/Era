param([string]$Version='0.29.2')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$eraProject=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraBuild=Join-Path $eraProject 'Builds/Windows'
$eraOut=Join-Path $eraProject "Releases/v$Version"
New-Item -ItemType Directory -Path $eraOut -Force | Out-Null
$eraArchive=Join-Path $eraOut 'Era-Windows.zip'
$eraPacked=Get-Content (Join-Path $eraProject 'Artifacts/player-content-v029.json') -Raw | ConvertFrom-Json
$eraTreeReportPath=Join-Path $eraProject 'Artifacts/WorldTree/world-tree.json'
$eraTreeReport=Get-Content $eraTreeReportPath -Raw | ConvertFrom-Json
if($eraPacked.assetCount -lt 1 -or -not $eraPacked.noAuthoringLibrary -or $eraPacked.version -ne $Version -or -not $eraTreeReport.ok -or $eraTreeReport.worlds.Count -ne 7){throw 'Player content audit and seven-realm runtime validation must pass first'}
if((Get-Item $eraTreeReportPath).LastWriteTime -lt (Get-Item (Join-Path $eraBuild 'Era_Data/Managed/Assembly-CSharp.dll')).LastWriteTime){throw 'Runtime report predates this Player build'}
if($Version -eq '0.29.2'){
    $eraGolemPath=Join-Path $eraProject 'Artifacts/GolemTree-v0292/golem-tree-runtime.json'
    $eraGolem=Get-Content -LiteralPath $eraGolemPath -Raw | ConvertFrom-Json
    if(-not $eraGolem.ok -or $eraGolem.version -ne $Version -or (Get-Item $eraGolemPath).LastWriteTime -lt (Get-Item (Join-Path $eraBuild 'Era_Data/Managed/Assembly-CSharp.dll')).LastWriteTime){throw 'Fresh golem / root runtime validation required'}
}
$eraFiles=[Collections.Generic.List[object]]::new()
foreach($eraTop in @('Era.exe','UnityPlayer.dll','UnityCrashHandler64.exe','Era_Data','MonoBleedingEdge','D3D12')){
    $eraPath=Join-Path $eraBuild $eraTop
    if(Test-Path -LiteralPath $eraPath){
        if((Get-Item -LiteralPath $eraPath).PSIsContainer){foreach($eraFile in Get-ChildItem -LiteralPath $eraPath -Recurse -File){$eraFiles.Add($eraFile)}}
        else{$eraFiles.Add((Get-Item -LiteralPath $eraPath))}
    }
}
foreach($eraName in @('Era.exe','UnityPlayer.dll','Era_Data/globalgamemanagers','Era_Data/Managed/Assembly-CSharp.dll','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll')){
    if(-not (Test-Path -LiteralPath (Join-Path $eraBuild $eraName))){throw "Missing Player dependency: $eraName"}
}
$eraStage=Join-Path $eraOut 'PackageMetadata'
New-Item -ItemType Directory -Path $eraStage -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $eraProject 'Docs/PLAY-RU.md') -Destination (Join-Path $eraStage 'PLAY-RU.md') -Force
$eraReadme="ERA: Seven Realms v$Version`r`n`r`nРаспакуйте весь архив и запустите Era.exe. Не переносите EXE отдельно от Era_Data и UnityPlayer.dll.`r`n`r`nИггдрасиль находится в центре каждого из семи миров. Исходники Blender, библиотека персонажей, отчёты и исторические рендеры в эту игровую сборку не включены.`r`nУправление — PLAY-RU.md.`r`n"
[IO.File]::WriteAllText((Join-Path $eraStage 'START-RU.txt'),$eraReadme,[Text.UTF8Encoding]::new($false))
$eraManifest=@{version=$Version;noAuthoringLibrary=$true;files=@()}
foreach($eraFile in $eraFiles){
    $eraName=$eraFile.FullName.Substring($eraBuild.Length+1).Replace('\','/')
    if($eraName -match '(?i)(ArtSource|CharacterFactory|ERA_CharacterLibrary|\.blend\d?$|\.fbx$|\.py$|\.ps1$)'){throw "Forbidden authoring file: $eraName"}
    $eraManifest.files+=@{name=$eraName;bytes=$eraFile.Length;sha256=(Get-FileHash -LiteralPath $eraFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$eraManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $eraStage 'PlayerContents.json') -Encoding utf8
if(Test-Path -LiteralPath $eraArchive){throw "Versioned archive already exists: $eraArchive; do not overwrite a published release"}
$eraZip=[IO.Compression.ZipFile]::Open($eraArchive,[IO.Compression.ZipArchiveMode]::Create)
try{
    foreach($eraFile in $eraFiles){$eraName=$eraFile.FullName.Substring($eraBuild.Length+1).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($eraZip,$eraFile.FullName,$eraName,[IO.Compression.CompressionLevel]::Optimal) | Out-Null}
    foreach($eraFile in Get-ChildItem -LiteralPath $eraStage -File){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($eraZip,$eraFile.FullName,$eraFile.Name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null}
    $eraThirdParty=Join-Path $eraProject 'Docs/THIRD-PARTY.md'
    if(Test-Path -LiteralPath $eraThirdParty){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($eraZip,$eraThirdParty,'Licenses/THIRD-PARTY.md') | Out-Null}
    foreach($eraLicense in Get-ChildItem (Join-Path $eraProject 'Assets/Era/Art/KayKit') -Filter 'LICENSE-*.txt'){
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($eraZip,$eraLicense.FullName,('Licenses/'+$eraLicense.Name)) | Out-Null
    }
}finally{$eraZip.Dispose()}
$eraHash=(Get-FileHash -LiteralPath $eraArchive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($eraArchive+'.sha256',"$eraHash  Era-Windows.zip`n",[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'Verify-Package.ps1') -Archive $eraArchive
Write-Output "Ready: $eraArchive"
