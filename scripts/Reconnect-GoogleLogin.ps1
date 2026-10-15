param([switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$serverRoot = Join-Path $repoRoot 'src\DuyTien.AssetManagement.Server'
$executable = Join-Path $serverRoot 'bin\Release\net10.0\DuyTien.AssetManagement.Server.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Prepare Release build first; no build/download is run here.' }
if (@(Get-NetTCPConnection -LocalPort 7008 -State Listen -ErrorAction SilentlyContinue).Count -gt 0) { & (Join-Path $PSScriptRoot 'Stop-Web.ps1') }
if (@(Get-CimInstance Win32_Process | Where-Object Name -eq 'DuyTien.AssetManagement.Server.exe').Count -gt 0) { throw 'Another backend/CLI exists. No second instance or consent started.' }
$saved = @{}
foreach ($key in @('DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','Storage__Mode')) { $saved[$key] = [Environment]::GetEnvironmentVariable($key,'Process') }
Push-Location $serverRoot
try {
    $env:DOTNET_ENVIRONMENT='Development'; $env:ASPNETCORE_ENVIRONMENT='Development'; $env:Storage__Mode='Google'
    # Native stderr is safe structured diagnostic JSON. Capture it for classification only.
    $ErrorActionPreference='Continue'
    $lines = @(& $executable auth-diagnose 2>&1 | ForEach-Object { $_.ToString() })
    $diagnosticExit = $LASTEXITCODE
    $ErrorActionPreference='Stop'
    $failure = $null
    foreach ($line in $lines) {
        if (-not $line.StartsWith('{')) { continue }
        try { $entry = $line | ConvertFrom-Json } catch { continue }
        if ($entry.failure) { $failure = $entry.failure }
        if ($entry.diagnostic -or $entry.failure) { Write-Output $line }
    }
    if ($diagnosticExit -ne 0) {
        if (-not $failure -or $failure.FailureCode -ne 'invalid_grant') { throw 'Auth diagnostic failed without invalid_grant; consent was NOT started. Inspect safe diagnostic metadata.' }
        Write-Output 'Google confirmed invalid_grant. Select the SAME DEV Google account in the browser and consent to drive.file only. No application admin reset.'
        & $executable google-oauth-bootstrap
        if ($LASTEXITCODE -ne 0) { throw 'Consent did not complete. No admin/resource initialization was attempted.' }
        & $executable auth-diagnose
        if ($LASTEXITCODE -ne 0) { throw 'Consent completed but auth store is not yet verified; do not reset or provision resources.' }
    }
    Write-Output 'Central auth read succeeded. Enter the existing admin password directly on the web form.'
}
finally {
    Pop-Location
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key,$saved[$key],'Process') }
    # Stop-Web and Start-Web validate the recorded PID/exe/start time and never kill by name.
    & (Join-Path $PSScriptRoot 'Start-Web.ps1') -NoBrowser:$NoBrowser
}
