param(
    [string]$Tag = 'v0.16.0',
    [string]$Repository = 'Regardov/Era',
    [string]$NotesFile = 'Docs/RELEASE-v0.16.0.md'
)
$ErrorActionPreference = 'Stop'
$eraProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $eraProject
try {
    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$' -or $Tag -notmatch '^v\d+\.\d+\.\d+$') { throw 'Invalid repository or version tag.' }
    # Website has its own Git repository and is published separately from the game.
    if (git status --porcelain -- . ':!Website') { throw 'Commit all game project changes before publishing.' }
    $eraCommit = git rev-parse "$Tag^{commit}"
    if ($LASTEXITCODE -ne 0) { throw 'Create and push the release tag first.' }
    $eraRemoteTag = git ls-remote "https://github.com/$Repository.git" "refs/tags/$Tag"
    if ($LASTEXITCODE -ne 0 -or $eraRemoteTag -notmatch "^$eraCommit\s") { throw 'The remote release tag must match the local commit.' }
    $eraArchive = Join-Path $eraProject 'Releases\Era-Windows.zip'
    $eraChecksum = $eraArchive + '.sha256'
    $eraHash = (Get-FileHash -LiteralPath $eraArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ((Get-Content -LiteralPath $eraChecksum -Raw).Trim() -ne "$eraHash  Era-Windows.zip") { throw 'Repackage the game: checksum does not match.' }
    $eraNotes = Get-Content -LiteralPath $NotesFile -Raw
    # Reuse Git Credential Manager in memory. Never write credentials to the project or logs.
    $eraCredentials = "protocol=https`nhost=github.com`n`n" | git credential fill
    if ($LASTEXITCODE -ne 0) { throw 'Sign in to GitHub with Git Credential Manager first.' }
    $eraSecret = ($eraCredentials | Where-Object { $_.StartsWith('password=') }).Substring(9)
    $eraHeaders = @{ Authorization = "Bearer $eraSecret"; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
    $eraApi = "https://api.github.com/repos/$Repository"
    $eraRepo = Invoke-RestMethod -Uri $eraApi -Headers $eraHeaders
    $eraReleases = Invoke-RestMethod -Uri "$eraApi/releases" -Headers $eraHeaders
    if ($eraReleases | Where-Object { $_.tag_name -eq $Tag }) { throw 'A release with this tag already exists; it will not be overwritten.' }
    $eraBody = @{ tag_name=$Tag; target_commitish=$eraCommit; name="ERA: Seven Realms $Tag — Windows"; body=$eraNotes; draft=$true; prerelease=$false } | ConvertTo-Json
    $eraRelease = Invoke-RestMethod -Method Post -Uri "$eraApi/releases" -Headers $eraHeaders -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($eraBody))
    $eraUploaded = @()
    foreach ($eraFile in @($eraArchive,$eraChecksum)) {
        $eraItem = Get-Item -LiteralPath $eraFile
        $eraUploadUri = $eraRelease.upload_url.Split('{')[0] + '?name=' + [Uri]::EscapeDataString($eraItem.Name)
        $eraAsset = Invoke-RestMethod -Method Post -Uri $eraUploadUri -Headers $eraHeaders -ContentType 'application/octet-stream' -InFile $eraFile -TimeoutSec 600
        if ($eraAsset.size -ne $eraItem.Length -or $eraAsset.state -ne 'uploaded') { throw "Incomplete upload: $($eraItem.Name). The release is still a draft." }
        if ($eraFile -eq $eraArchive -and $eraAsset.digest -and $eraAsset.digest -ne "sha256:$eraHash") { throw 'GitHub archive digest differs; the release is still a draft.' }
        $eraUploaded += $eraAsset | Select-Object id,name,size,digest,browser_download_url
        Write-Output "Uploaded $($eraAsset.name) ($($eraAsset.size) bytes)"
    }
    # Publish only after both complete uploads. Repository visibility is never changed.
    $eraPublished = Invoke-RestMethod -Method Patch -Uri "$eraApi/releases/$($eraRelease.id)" -Headers $eraHeaders -ContentType 'application/json' -Body '{"draft":false,"make_latest":"true"}'
    $eraUploaded = $eraPublished.assets | Select-Object id,name,size,digest,browser_download_url
    New-Item -ItemType Directory -Path 'Artifacts' -Force | Out-Null
    @{tag=$Tag;commit=$eraCommit;url=$eraPublished.html_url;private=$eraRepo.private;draft=$eraPublished.draft;assets=$eraUploaded} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "Artifacts/release-$Tag.json" -Encoding utf8
    Write-Output "Published $($eraPublished.html_url); private=$($eraRepo.private)"
} finally {
    Remove-Variable eraSecret,eraCredentials,eraHeaders -ErrorAction SilentlyContinue
    Pop-Location
}
