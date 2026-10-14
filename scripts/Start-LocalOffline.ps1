param([string]$Workspace=(Join-Path $env:LOCALAPPDATA 'DuyTienAssets\LocalOffline\default'),[ValidateRange(1024,65535)][int]$Port=7008,[switch]$NoBrowser)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath=[IO.Path]::GetFullPath($Workspace)
if(-not [IO.Path]::IsPathRooted($Workspace) -or $workspacePath.StartsWith($repoRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $workspacePath -eq $repoRoot -or $workspacePath.StartsWith('\\')){throw 'Workspace must be an absolute local path outside Git.'}
$exePath=Join-Path $repoRoot 'src\DuyTien.AssetManagement.Server\bin\Release\net10.0\DuyTien.AssetManagement.Server.exe'
if(-not (Test-Path -LiteralPath $exePath)){throw 'Release output missing. Run the documented prepare/build command once; launcher never builds or downloads.'}
$mutex=[Threading.Mutex]::new($false,'Local\DuyTienAssets-LocalOfflineLauncher')
if(-not $mutex.WaitOne(0)){ $mutex.Dispose(); throw 'Another LocalOffline launcher is starting. Retry after it finishes.' }
try {
    $url='https://localhost:'+$Port
    $listeners=@(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    if($listeners.Count -gt 0){
        $manifestPath=Join-Path $workspacePath 'manifest.json'
        if(-not (Test-Path -LiteralPath $manifestPath)){throw 'Port is occupied; no matching workspace. No second backend started.'}
        $manifest=Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        $status=Invoke-RestMethod ($url+'/api/system/status') -TimeoutSec 10
        if($status.mode -ne 'LocalOffline' -or $status.workspaceId -ne $manifest.WorkspaceId){throw 'Port belongs to another backend/workspace. No process stopped.'}
        Write-Output ('Reused LocalOffline backend at '+$url)
        if(-not $NoBrowser){Start-Process ($url+'/')}
        return
    }
    $backends=@(Get-CimInstance Win32_Process | Where-Object Name -eq 'DuyTien.AssetManagement.Server.exe')
    if($backends.Count -gt 0){throw 'An existing DuyTien backend must be stopped explicitly before LocalOffline starts. No process stopped.'}
    [IO.Directory]::CreateDirectory($workspacePath) | Out-Null
    if(([IO.File]::GetAttributes($workspacePath) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Workspace cannot be a reparse point.'}
    $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
    $security=[Security.AccessControl.DirectorySecurity]::new()
    $security.SetOwner($identity.User);$security.SetAccessRuleProtection($true,$false)
    foreach($sid in @($identity.User,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){$security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
    $directory=[IO.DirectoryInfo]::new($workspacePath)
    if($directory.PSObject.Methods['SetAccessControl']){$directory.SetAccessControl($security)}else{[IO.FileSystemAclExtensions]::SetAccessControl($directory,$security)}
    $oldMode=$env:Storage__Mode;$oldWorkspace=$env:Storage__WorkspacePath;$oldUrls=$env:ASPNETCORE_URLS;$oldEnvironment=$env:ASPNETCORE_ENVIRONMENT
    try {
        $env:Storage__Mode='LocalOffline';$env:Storage__WorkspacePath=$workspacePath;$env:ASPNETCORE_URLS=$url;$env:ASPNETCORE_ENVIRONMENT='Development'
        $process=Start-Process -FilePath $exePath -WorkingDirectory (Join-Path $repoRoot 'src\DuyTien.AssetManagement.Server') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $workspacePath 'backend.stdout.log') -RedirectStandardError (Join-Path $workspacePath 'backend.stderr.log') -PassThru
    } finally {$env:Storage__Mode=$oldMode;$env:Storage__WorkspacePath=$oldWorkspace;$env:ASPNETCORE_URLS=$oldUrls;$env:ASPNETCORE_ENVIRONMENT=$oldEnvironment}
    $started=$process.StartTime.ToUniversalTime().ToString('o')
    @{Pid=$process.Id;StartedUtc=$started;Executable=$exePath;Mode='LocalOffline';Workspace=$workspacePath;Url=$url}|ConvertTo-Json|Set-Content -Encoding UTF8 -LiteralPath (Join-Path $workspacePath 'backend-process.json')
    $ready=$false
    for($i=0;$i -lt 40;$i++){
        if($process.HasExited){throw ('LocalOffline exited '+$process.ExitCode+'. Inspect private workspace logs; do not paste raw secrets.')}
        try{$status=Invoke-RestMethod ($url+'/api/system/status') -TimeoutSec 2;if($status.mode -eq 'LocalOffline'){$ready=$true;break}}catch [System.Net.WebException]{}
        Start-Sleep -Milliseconds 250
    }
    if(-not $ready){throw 'Backend did not become HTTPS-ready. Do not bypass TLS; inspect private logs and dev-certs trust.'}
    Write-Output ('LocalOffline running: '+$url+' ; PID '+$process.Id+' ; workspace '+$workspacePath)
    if(-not $NoBrowser){Start-Process ($url+'/')}
} finally {$mutex.ReleaseMutex();$mutex.Dispose()}
