$ErrorActionPreference='Stop'
$projectPath=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$openedDirectly=$false
# Use the same local Hub IPC route as Unity's own project launcher. It also registers recents.
$pipe=[System.IO.Pipes.NamedPipeClientStream]::new('.','Unity-hubIPCService',[System.IO.Pipes.PipeDirection]::InOut,[System.IO.Pipes.PipeOptions]::Asynchronous)
try {
    $pipe.Connect(3000)
    $packet=([ordered]@{type='project:open';data=@{path=$projectPath}}|ConvertTo-Json -Compress)+[char]12
    $bytes=[System.Text.Encoding]::UTF8.GetBytes($packet)
    $pipe.Write($bytes,0,$bytes.Length)
    $pipe.Flush()
    $buffer=[byte[]]::new(8192)
    $reply=$pipe.ReadAsync($buffer,0,$buffer.Length)
    if($reply.Wait(8000)) {
        $message=[System.Text.Encoding]::UTF8.GetString($buffer,0,$reply.Result).Trim([char]12)|ConvertFrom-Json
        Write-Output "Unity Hub: $($message.type) = $($message.data)"
    } else {
        Write-Output 'Open request sent to Unity Hub. The editor may still be loading.'
    }
} catch {
    Write-Output 'Unity Hub launcher unavailable. Opening the installed editor directly.'
    & (Join-Path $PSScriptRoot 'Open-Unity.ps1')
    $openedDirectly=$true
} finally {
    $pipe.Dispose()
}
if(-not $openedDirectly) {
    $found=$false
    for($attempt=0;$attempt -lt 20;$attempt++) {
        $editors=Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" -ErrorAction SilentlyContinue
        foreach($editor in $editors) {
            if($editor.CommandLine -and $editor.CommandLine.Replace('/','\').IndexOf($projectPath,[StringComparison]::OrdinalIgnoreCase) -ge 0){$found=$true}
        }
        if($found){break}
        Start-Sleep -Milliseconds 250
    }
    if(-not $found) {
        Write-Output 'Hub accepted the request but did not launch this project. Opening Unity directly.'
        & (Join-Path $PSScriptRoot 'Open-Unity.ps1')
    }
}
