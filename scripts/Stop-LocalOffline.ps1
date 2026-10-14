param([string]$Workspace=(Join-Path $env:LOCALAPPDATA 'DuyTienAssets\LocalOffline\default'))
$ErrorActionPreference='Stop'
$workspacePath=[IO.Path]::GetFullPath($Workspace)
$record=Get-Content -Raw -LiteralPath (Join-Path $workspacePath 'backend-process.json') | ConvertFrom-Json
if($record.Mode -ne 'LocalOffline' -or [IO.Path]::GetFullPath($record.Workspace) -ne $workspacePath){throw 'Process record mismatch; no process stopped.'}
$owned=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$record.Pid)
if(-not $owned){Write-Output 'Recorded backend already stopped.';return}
$process=Get-Process -Id $record.Pid
if($owned.ExecutablePath -ne $record.Executable -or $process.StartTime.ToUniversalTime().ToString('o') -ne $record.StartedUtc){throw 'PID/executable/start-time mismatch; no process stopped.'}
Stop-Process -Id $process.Id
Write-Output ('Stopped owned LocalOffline PID '+$process.Id)
