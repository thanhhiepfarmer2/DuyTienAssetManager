param([string]$CachePath=(Join-Path $env:LOCALAPPDATA 'DuyTienAssets\ConnectedCache'))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($CachePath)
$record=Get-Content -LiteralPath (Join-Path $root 'web-process.json') -Raw|ConvertFrom-Json
if($record.Mode -ne 'Connected' -or $record.CachePath -ne $root){throw 'Process record mismatch; no process stopped.'}
$owned=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$record.Pid)
if(-not $owned){Write-Output 'Recorded backend already stopped.';return}
$process=Get-Process -Id $record.Pid
if($owned.ExecutablePath -ne $record.Executable -or $process.StartTime.ToUniversalTime().ToString('o') -ne $record.StartedUtc){throw 'PID/executable/start time mismatch; no process stopped.'}
Stop-Process -Id $process.Id
Write-Output ('Stopped owned Connected backend '+$process.Id)
