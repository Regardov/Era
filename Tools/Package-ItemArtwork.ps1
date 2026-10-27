$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraPng=Join-Path $eraRoot 'Assets/Era/Resources/Items/v025'
$eraSvg=Join-Path $eraRoot 'ArtSource/Items/v0.25'
$eraStage=Join-Path $eraRoot 'Artifacts/ItemArtworkPackage-v0.25.0'
foreach($eraDirectory in @('PNG','SVG','Preview')){New-Item -ItemType Directory -Path (Join-Path $eraStage $eraDirectory) -Force | Out-Null}
$eraPngFiles=@(Get-ChildItem -LiteralPath $eraPng -Filter '*.png' -File)
$eraSvgFiles=@(Get-ChildItem -LiteralPath $eraSvg -Filter '*.svg' -File)
if($eraPngFiles.Count -ne 129 -or $eraSvgFiles.Count -ne 129){throw 'Artwork package requires all 129 PNG and SVG objects'}
$eraPngFiles | Copy-Item -Destination (Join-Path $eraStage 'PNG') -Force
$eraSvgFiles | Copy-Item -Destination (Join-Path $eraStage 'SVG') -Force
Get-ChildItem -LiteralPath $eraSvg -Filter 'ContactSheet-*.png' -File | Copy-Item -Destination (Join-Path $eraStage 'Preview') -Force
$eraReadme=@'
# ERA v0.25 — предметы без фона

PNG/: 129 отдельных RGBA PNG 256x256 с прозрачным фоном.
SVG/: 129 редактируемых векторных исходников с теми же именами.
Preview/: четыре обзорных листа всех рисунков.

Имена совпадают со стабильными ItemId игры. В текущем выпадении участвуют 116 предметов; 13 зарезервированных старых ID нарисованы дополнительно, но не возвращены в лут. Изображения содержат только предмет, без карточки, текста и рамки. Свечение редкости создаётся отдельно средствами игры.

Все иллюстрации оригинальные, созданы векторным генератором проекта Assets/Era/Editor/ItemArtworkBuilder.cs. Шрифты и платные наборы для их использования не требуются.
'@
[IO.File]::WriteAllText((Join-Path $eraStage 'README-RU.md'),$eraReadme,[Text.UTF8Encoding]::new($false))
$eraArchive=Join-Path $eraRoot 'Releases/Era-Item-Artwork-v0.25.0.zip'
Compress-Archive -Path (Join-Path $eraStage '*') -DestinationPath $eraArchive -Force
$eraZip=[IO.Compression.ZipFile]::OpenRead($eraArchive)
try{
    foreach($eraFile in @($eraPngFiles)+@($eraSvgFiles)){
        $eraFolder=if($eraFile.Extension -eq '.png'){'PNG/'}else{'SVG/'}
        $eraEntry=$eraZip.GetEntry($eraFolder+$eraFile.Name)
        if($null -eq $eraEntry -or $eraEntry.Length -ne $eraFile.Length){throw "Missing/truncated artwork: $($eraFile.Name)"}
        $eraStream=$eraEntry.Open();$eraSha=[Security.Cryptography.SHA256]::Create()
        try{$eraInside=[BitConverter]::ToString($eraSha.ComputeHash($eraStream)).Replace('-','')}
        finally{$eraStream.Dispose();$eraSha.Dispose()}
        if($eraInside -ne (Get-FileHash -LiteralPath $eraFile.FullName -Algorithm SHA256).Hash){throw 'Artwork archive content mismatch'}
    }
}finally{$eraZip.Dispose()}
$eraHash=(Get-FileHash -LiteralPath $eraArchive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($eraArchive+'.sha256',"$eraHash  Era-Item-Artwork-v0.25.0.zip`n",[Text.UTF8Encoding]::new($false))
Write-Output "PASS Item artwork archive: 129 PNG + 129 SVG; sha256=$eraHash"
