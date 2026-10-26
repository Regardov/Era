$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraArchivePath = Join-Path $eraProject 'Releases\Era-Windows.zip'
$eraBuildPath = Join-Path $eraProject 'Builds\Windows'
$eraZip = [IO.Compression.ZipFile]::OpenRead($eraArchivePath)
try {
    foreach ($eraName in @('Era.exe','UnityPlayer.dll','Era_Data/Managed/Assembly-CSharp.dll','PLAY-RU.md','DOWNLOAD-RU.md','RELEASE-v0.22.0.md','VALIDATION-v0.22.md','ARCHER-MOTION.md','archer-runtime-v022.json','archer-motion-release-v022.json','campaign-smoke-v022.json','FEEDBACK-VALIDATION.md','MECHANIC-FEEDBACK-AUDIT.md')) {
        $eraEntry = $eraZip.GetEntry($eraName)
        if ($null -eq $eraEntry -or $eraEntry.Length -le 0) { throw "Missing or empty ZIP entry: $eraName" }
    }
    $eraRuntimeReader = [IO.StreamReader]::new($eraZip.GetEntry('archer-runtime-v022.json').Open())
    try { $eraRuntime = $eraRuntimeReader.ReadToEnd() | ConvertFrom-Json }
    finally { $eraRuntimeReader.Dispose() }
    if (-not $eraRuntime.ok -or $eraRuntime.version -ne '0.22.0') { throw 'ZIP must contain validation from the tested 0.22.0 Player' }
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
