$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraArchivePath = Join-Path $eraProject 'Releases\Era-Windows.zip'
$eraBuildPath = Join-Path $eraProject 'Builds\Windows'
$eraZip = [IO.Compression.ZipFile]::OpenRead($eraArchivePath)
try {
    foreach ($eraName in @('Era.exe','UnityPlayer.dll','Era_Data/Managed/Assembly-CSharp.dll','Era_Data/globalgamemanagers','Era_Data/level0','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','ANIMATION-QUALITY-AUDIT.md','AnimationQuality/clip-status.csv','PLAY-RU.md','DOWNLOAD-RU.md','ITEMS-v0.24.md','RELEASE-v0.24.0.md','VALIDATION-v0.24.md','balance-runtime-v024.json','loot-balance-validation-v024.json','ARCHER-MOTION.md','FEEDBACK-VALIDATION.md','MECHANIC-FEEDBACK-AUDIT.md')) {
        $eraEntry = $eraZip.GetEntry($eraName)
        if ($null -eq $eraEntry -or $eraEntry.Length -le 0) { throw "Missing or empty ZIP entry: $eraName" }
    }
    $eraRuntimeReader = [IO.StreamReader]::new($eraZip.GetEntry('balance-runtime-v024.json').Open())
    try { $eraRuntime = $eraRuntimeReader.ReadToEnd() | ConvertFrom-Json }
    finally { $eraRuntimeReader.Dispose() }
    if (-not $eraRuntime.ok -or $eraRuntime.version -ne '0.24.0' -or $eraRuntime.activeItems -ne 116 -or $eraRuntime.rareItems -ne 10 -or $eraRuntime.chests -ne 30) { throw 'ZIP must contain loot and chest validation from the tested 0.24.0 Player' }
    $eraMergedReader = [IO.StreamReader]::new($eraZip.GetEntry('loot-balance-validation-v024.json').Open())
    try { $eraMergedValidation = $eraMergedReader.ReadToEnd() | ConvertFrom-Json }
    finally { $eraMergedReader.Dispose() }
    $eraAssemblyHash = (Get-FileHash -LiteralPath (Join-Path $eraBuildPath 'Era_Data\Managed\Assembly-CSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($eraMergedValidation.version -ne '0.24.0' -or $eraMergedValidation.managedSha256 -ne $eraAssemblyHash) { throw 'Loot balance validation belongs to a different Player build' }
    $eraGameDataHash = (Get-FileHash -LiteralPath (Join-Path $eraBuildPath 'Era_Data/globalgamemanagers') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($eraMergedValidation.gameDataSha256 -ne $eraGameDataHash) { throw 'Versioned game data differs from the tested build' }
    foreach ($eraTest in $eraMergedValidation.tests.PSObject.Properties) { if (-not $eraTest.Value.ok) { throw "Failed merged validation: $($eraTest.Name)" } }
    $eraPlayerFiles = Get-ChildItem -LiteralPath $eraBuildPath -Recurse -File
    foreach ($eraPlayerFile in $eraPlayerFiles) {
        $eraName = $eraPlayerFile.FullName.Substring($eraBuildPath.Length+1).Replace('\','/')
        $eraEntry = $eraZip.GetEntry($eraName)
        if ($null -eq $eraEntry -or $eraEntry.Length -ne $eraPlayerFile.Length) { throw "Missing or truncated tested build file: $eraName" }
        $eraStream = $eraEntry.Open()
        $eraSha = [Security.Cryptography.SHA256]::Create()
        try { $eraInsideHash = [BitConverter]::ToString($eraSha.ComputeHash($eraStream)).Replace('-','') }
        finally { $eraStream.Dispose(); $eraSha.Dispose() }
        $eraPlayerHash = (Get-FileHash -LiteralPath $eraPlayerFile.FullName -Algorithm SHA256).Hash
        if ($eraInsideHash -ne $eraPlayerHash) { throw "ZIP differs from the tested Player: $eraName" }
    }
    $eraEntries = $eraZip.Entries.Count
    if ($eraEntries -lt 100) { throw "Incomplete Player archive: $eraEntries entries" }
} finally { $eraZip.Dispose() }
$eraArchiveHash = (Get-FileHash -LiteralPath $eraArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-Content -LiteralPath ($eraArchivePath + '.sha256') -Raw).Trim() -ne "$eraArchiveHash  Era-Windows.zip") { throw 'ZIP checksum differs from its companion file' }
Write-Output "PASS Package: entries=$eraEntries; all $($eraPlayerFiles.Count) tested build files match; sha256=$eraArchiveHash"
