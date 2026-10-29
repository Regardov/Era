param([ValidateRange(10,360)][int]$Fps=150,[switch]$Record)
$ErrorActionPreference='Stop'
$eraRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$eraFolder=Join-Path $eraRoot "Artifacts/ArcherRefinement-$Fps"
New-Item -ItemType Directory -Force -Path $eraFolder | Out-Null
$eraReport=Join-Path $eraFolder 'refinement-runtime.json'
if(Test-Path -LiteralPath $eraReport){Remove-Item -LiteralPath $eraReport}
$eraArgs=@('--smoke-archer-refinement','--refinement-fps',"$Fps",'-job-worker-count','1','-screen-width','960','-screen-height','720','-screen-fullscreen','0','--capture-folder',"`"$eraFolder`"",'-logFile',"`"$eraFolder/player.log`"")
if($Record){$eraArgs+='--refinement-record'}
$eraProcess=Start-Process -FilePath (Join-Path $eraRoot 'Builds/Windows/Era.exe') -ArgumentList $eraArgs -PassThru -WindowStyle Hidden
try{
    $eraProcess.PriorityClass='BelowNormal'
    if(-not $eraProcess.WaitForExit(100000)){throw 'Archer refinement runtime test timed out'}
    $eraResult=Get-Content -LiteralPath $eraReport -Raw | ConvertFrom-Json
    $eraShots=@($eraResult.phases | Where-Object shots -GT 0)
    # Render root intentionally trails authority by at most one simulation tick.
    # Check exact agreement with the presentation clock, and socket relative to
    # that visible root; retain raw root/muzzle gaps in the report for review.
    # maxSpeed is planar only: it cannot bound the 3D lag of a jumping actor.
    # Exact presentation-clock agreement checks both horizontal and vertical pose.
    if(@($eraShots | Where-Object { $_.maxShotPresentationError -gt .001 -or $_.maxShotDirectionError -gt 1 -or $_.maxShotLocalOriginGap -gt .15 }).Count){throw 'Release socket exceeds the documented interpolated visual alignment bounds'}
    $eraResult.phases | Select-Object name,airFrames,shots,airShots,maxRate,cyclesPerSecond,maxPlantError | Format-Table
    if(-not $eraResult.ok -or $eraProcess.ExitCode -ne 0){throw "Refinement check failed: $($eraResult.failures -join '; '); see $eraReport"}
    Write-Output "PASS ArcherRefinement: FPS=$Fps, jumps=$($eraResult.jumps), landings=$($eraResult.landings), shots=$($eraResult.shots), events=$($eraResult.events)"
}finally{if(-not $eraProcess.HasExited){Stop-Process -Id $eraProcess.Id}}
