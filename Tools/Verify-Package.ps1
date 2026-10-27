$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraArchivePath = Join-Path $eraProject 'Releases\Era-Windows.zip'
$eraBuildPath = Join-Path $eraProject 'Builds\Windows'
$eraZip = [IO.Compression.ZipFile]::OpenRead($eraArchivePath)
try {
    foreach ($eraName in @('Era.exe','UnityPlayer.dll','Era_Data/Managed/Assembly-CSharp.dll','Era_Data/globalgamemanagers','Era_Data/level0','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','PLAY-RU.md','DOWNLOAD-RU.md','RELEASE-v0.23.1.md','VALIDATION-v0.23.1.md','ARCHER-MOTION.md','archer-runtime-v0231.json','archer-quality-v0231.json','campaign-smoke-v0231.json','release-validation-v0231.json','ANIMATION-QUALITY-AUDIT.md','AnimationQuality/clip-status.csv','FEEDBACK-VALIDATION.md','MECHANIC-FEEDBACK-AUDIT.md')) {
        $eraEntry = $eraZip.GetEntry($eraName)
        if ($null -eq $eraEntry -or $eraEntry.Length -le 0) { throw "Missing or empty ZIP entry: $eraName" }
    }
    $eraRuntimeReader = [IO.StreamReader]::new($eraZip.GetEntry('archer-runtime-v0231.json').Open())
    try { $eraRuntime = $eraRuntimeReader.ReadToEnd() | ConvertFrom-Json }
    finally { $eraRuntimeReader.Dispose() }
    if (-not $eraRuntime.ok -or $eraRuntime.version -ne '0.23.1') { throw 'ZIP must contain validation from the tested 0.23.1 Player' }
    $eraMergedReader = [IO.StreamReader]::new($eraZip.GetEntry('release-validation-v0231.json').Open())
    try { $eraMergedValidation = $eraMergedReader.ReadToEnd() | ConvertFrom-Json }
    finally { $eraMergedReader.Dispose() }
    $eraAssemblyHash = (Get-FileHash -LiteralPath (Join-Path $eraBuildPath 'Era_Data\Managed\Assembly-CSharp.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($eraMergedValidation.version -ne '0.23.1' -or $eraMergedValidation.managedSha256 -ne $eraAssemblyHash) { throw 'Release validation belongs to a different Player build' }
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
