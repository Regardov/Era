param([string]$Archive='Releases/v0.29.0/Era-Windows.zip')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$eraProject=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(-not [IO.Path]::IsPathRooted($Archive)){$Archive=Join-Path $eraProject $Archive}
$eraZip=[IO.Compression.ZipFile]::OpenRead($Archive)
try{
    foreach($eraName in @('Era.exe','UnityPlayer.dll','Era_Data/globalgamemanagers','Era_Data/level0','Era_Data/Managed/Assembly-CSharp.dll','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','PlayerContents.json','START-RU.txt','PLAY-RU.md')){
        $eraEntry=$eraZip.GetEntry($eraName);if($null -eq $eraEntry -or $eraEntry.Length -le 0){throw "Missing runtime file: $eraName"}
    }
    foreach($eraEntry in $eraZip.Entries){if($eraEntry.FullName -match '(?i)(ArtSource|CharacterFactory|ERA_CharacterLibrary|\.blend\d?$|\.fbx$|\.py$|\.ps1$|\.cs$)'){throw "Authoring source in game ZIP: $($eraEntry.FullName)"}}
    $eraReader=[IO.StreamReader]::new($eraZip.GetEntry('PlayerContents.json').Open())
    try{$eraManifest=$eraReader.ReadToEnd() | ConvertFrom-Json}finally{$eraReader.Dispose()}
    if(-not $eraManifest.noAuthoringLibrary){throw 'Missing authoring-library exclusion audit'}
    foreach($eraFile in $eraManifest.files){
        $eraEntry=$eraZip.GetEntry($eraFile.name);if($null -eq $eraEntry -or $eraEntry.Length -ne $eraFile.bytes){throw "Truncated runtime file: $($eraFile.name)"}
        $eraStream=$eraEntry.Open();$eraSha=[Security.Cryptography.SHA256]::Create()
        try{$eraHash=[BitConverter]::ToString($eraSha.ComputeHash($eraStream)).Replace('-','').ToLowerInvariant()}finally{$eraStream.Dispose();$eraSha.Dispose()}
        if($eraHash -ne $eraFile.sha256){throw "ZIP hash mismatch: $($eraFile.name)"}
        $eraDisk=Join-Path $eraProject ('Builds/Windows/'+$eraFile.name)
        if((Get-FileHash -LiteralPath $eraDisk -Algorithm SHA256).Hash.ToLowerInvariant() -ne $eraHash){throw "ZIP differs from tested Player: $($eraFile.name)"}
    }
    $eraResult=@{ok=$true;version=$eraManifest.version;runtimeFiles=$eraManifest.files.Count;zipEntries=$eraZip.Entries.Count;noAuthoringLibrary=$true;zipBytes=(Get-Item -LiteralPath $Archive).Length}
}finally{$eraZip.Dispose()}
$eraHash=(Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant()
if((Get-Content -LiteralPath ($Archive+'.sha256') -Raw).Trim() -ne "$eraHash  Era-Windows.zip"){throw 'Archive checksum mismatch'}
$eraResult.sha256=$eraHash
$eraResult | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $eraProject 'Artifacts/package-v029.json') -Encoding utf8
Write-Output ($eraResult | ConvertTo-Json -Compress)
