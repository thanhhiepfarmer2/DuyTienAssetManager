param([string]$CachePath=(Join-Path $env:LOCALAPPDATA 'DuyTienAssets\ConnectedCache'),[int]$Port=7008,[switch]$NoBrowser)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$cacheRoot=[IO.Path]::GetFullPath($CachePath)
if(-not [IO.Path]::IsPathRooted($CachePath) -or $cacheRoot.StartsWith($repoRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $cacheRoot -eq $repoRoot -or $cacheRoot.StartsWith('\\')){throw 'Cache requires an absolute local path outside Git.'}
for($directory=[IO.DirectoryInfo]::new($cacheRoot);$null -ne $directory;$directory=$directory.Parent){if($directory.Exists -and ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Cache cannot be under a reparse point.'}}
$exePath=Join-Path $repoRoot 'src\DuyTien.AssetManagement.Server\bin\Release\net10.0\DuyTien.AssetManagement.Server.exe'
if(-not (Test-Path -LiteralPath $exePath)){throw 'Release output missing. Prepare it once using COMMANDS; this launcher never builds/downloads.'}
$mutex=[Threading.Mutex]::new($false,'Local\DuyTienAssets-WebLauncher')
if(-not $mutex.WaitOne(0)){$mutex.Dispose();throw 'Another web launcher is starting.'}
try {
 $url='https://localhost:'+$Port
 $recordPath=Join-Path $cacheRoot 'web-process.json'
 $listeners=@(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
 if($listeners.Count -gt 0){
  if(-not (Test-Path -LiteralPath $recordPath)){throw 'An existing backend must be verified/stopped explicitly; no second backend started.'}
  $record=Get-Content -LiteralPath $recordPath -Raw|ConvertFrom-Json
  $owned=Get-CimInstance Win32_Process -Filter ('ProcessId='+[int]$record.Pid)
  $process=Get-Process -Id $record.Pid
  if($record.Mode -ne 'Connected' -or $record.Url -ne $url -or $record.CachePath -ne $cacheRoot -or $owned.ExecutablePath -ne $exePath -or $process.StartTime.ToUniversalTime().ToString('o') -ne $record.StartedUtc -or $listeners.OwningProcess -notcontains $process.Id){throw 'Existing process does not match launcher record; no process stopped.'}
  $status=Invoke-RestMethod ($url+'/api/system/status') -TimeoutSec 10
  if($status.mode -ne 'Connected'){throw 'Backend is not the expected Connected mode.'}
  Write-Output ('Reused one backend '+$url+' ; PID '+$process.Id)
  if(-not $NoBrowser){Start-Process ($url+'/Account/Login')}
  return
 }
 if(@(Get-CimInstance Win32_Process|Where-Object Name -eq 'DuyTien.AssetManagement.Server.exe').Count -gt 0){throw 'Another DuyTien backend exists; no second backend started.'}
 [IO.Directory]::CreateDirectory($cacheRoot)|Out-Null
 $identity=[Security.Principal.WindowsIdentity]::GetCurrent();$security=[Security.AccessControl.DirectorySecurity]::new();$security.SetOwner($identity.User);$security.SetAccessRuleProtection($true,$false)
 foreach($sid in @($identity.User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){$security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
 $directory=[IO.DirectoryInfo]::new($cacheRoot)
 if($directory.PSObject.Methods['SetAccessControl']){$directory.SetAccessControl($security)}else{[IO.FileSystemAclExtensions]::SetAccessControl($directory,$security)}
 $saved=@{};foreach($name in @('Storage__Mode','ConnectedCache__Enabled','ConnectedCache__Path','ASPNETCORE_URLS','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT')){$saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
 try {
  $env:Storage__Mode='Google';$env:ConnectedCache__Enabled='true';$env:ConnectedCache__Path=$cacheRoot;$env:ASPNETCORE_URLS=$url;$env:ASPNETCORE_ENVIRONMENT='Development';$env:DOTNET_ENVIRONMENT='Development'
  $process=Start-Process -FilePath $exePath -WorkingDirectory (Join-Path $repoRoot 'src\DuyTien.AssetManagement.Server') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $cacheRoot 'web.stdout.log') -RedirectStandardError (Join-Path $cacheRoot 'web.stderr.log') -PassThru
 }finally{foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],'Process')}}
 @{Pid=$process.Id;StartedUtc=$process.StartTime.ToUniversalTime().ToString('o');Executable=$exePath;Mode='Connected';CachePath=$cacheRoot;Url=$url}|ConvertTo-Json|Set-Content -LiteralPath $recordPath -Encoding UTF8
 $ready=$false
 for($i=0;$i -lt 40;$i++){
  if($process.HasExited){throw ('Backend exited '+$process.ExitCode+'. Inspect private logs without publishing credentials.')}
  try{$status=Invoke-RestMethod ($url+'/api/system/status') -TimeoutSec 8;if($status.mode -eq 'Connected'){$ready=$true;break}}catch [System.Net.WebException]{}
  Start-Sleep -Milliseconds 250
 }
 if(-not $ready){throw 'HTTPS startup failed; do not bypass TLS or start a second backend.'}
 Write-Output ('Web Google+read-only cache running '+$url+' ; PID '+$process.Id+' ; state '+$status.state)
 if(-not $NoBrowser){Start-Process ($url+'/Account/Login')}
}finally{$mutex.ReleaseMutex();$mutex.Dispose()}
