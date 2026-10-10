# TASK-005: bootstrap/no-op on verified Google DEV.
# Password/token/hash stay in RAM or user-secrets. Output only assertions/status.
# Web must be stopped; start it separately with the documented dotnet run command.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj'
$xml = [xml](Get-Content -LiteralPath $project -Raw)
$secretsFile = Join-Path ([Environment]::GetFolderPath('ApplicationData')) ('Microsoft\UserSecrets\' + $xml.Project.PropertyGroup.UserSecretsId + '\secrets.json')
$googleClient = $null
$assertions = 0
function Expect([string]$name, $actual, $expected) {
    $script:assertions++
    $passed = $actual -eq $expected
    [pscustomobject]@{Case=$name;Expected=$expected;Actual=$actual;Pass=$passed} | ConvertTo-Json -Compress
    if (-not $passed) { throw 'Unexpected UAT result.' }
}
function GoogleJson([string]$uri) {
    $r=$googleClient.GetAsync($uri).GetAwaiter().GetResult()
    try {
        if (-not $r.IsSuccessStatusCode) { throw 'Google read failed.' }
        return ($r.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
    } finally { $r.Dispose() }
}
function Snapshot {
    $query = @("'Users'!A2:Q", "'UserRoleAssignments'!A2:K", "'RoleAssignmentScopes'!A2:F") | ForEach-Object { 'ranges=' + [uri]::EscapeDataString($_) }
    return (GoogleJson ($sheetUrl + '/values:batchGet?' + ($query -join '&') + '&valueRenderOption=UNFORMATTED_VALUE'))
}
try {
    $cfg=Get-Content -LiteralPath $secretsFile -Raw | ConvertFrom-Json
    if (@(Get-Process -Name 'DuyTien.AssetManagement.Server' -ErrorAction SilentlyContinue).Count -gt 0 -or @(Get-NetTCPConnection -State Listen -LocalPort 7008,5211 -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Stop backend before bootstrap.' }
    if (-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('Auth__Bootstrap__AdminPassword'))) { throw 'Unexpected environment override.' }
    $bootstrapPassword=$cfg.'Auth:Bootstrap:AdminPassword'
    if ([string]::IsNullOrEmpty($bootstrapPassword)) { throw 'Bootstrap secret missing.' }
    & (Join-Path $PSScriptRoot 'Test-Task005GoogleDev.ps1')
    $preflightSucceeded=$?
    if (-not $preflightSucceeded) { throw 'DEV preflight failed.' }
    Add-Type -AssemblyName System.Net.Http
    $googleClient=[System.Net.Http.HttpClient]::new()
    $googleClient.Timeout=[TimeSpan]::FromSeconds(90)
    $form=[Collections.Generic.Dictionary[string,string]]::new()
    $form.Add('client_id',$cfg.'GoogleWorkspace:OAuthClientId'); $form.Add('client_secret',$cfg.'GoogleWorkspace:OAuthClientSecret')
    $form.Add('refresh_token',$cfg.'GoogleWorkspace:OAuthRefreshToken'); $form.Add('grant_type','refresh_token')
    $r=$googleClient.PostAsync('https://oauth2.googleapis.com/token',[System.Net.Http.FormUrlEncodedContent]::new($form)).GetAwaiter().GetResult()
    if (-not $r.IsSuccessStatusCode) { throw 'OAuth refresh failed.' }
    $grant=$r.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $r.Dispose()
    Expect 'OAuth exact drive.file' $grant.scope 'https://www.googleapis.com/auth/drive.file'
    $googleClient.DefaultRequestHeaders.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$grant.access_token)
    $sheetUrl='https://sheets.googleapis.com/v4/spreadsheets/' + [uri]::EscapeDataString($cfg.'GoogleWorkspace:SystemSpreadsheetId')
    & dotnet run --project $project --no-build --launch-profile https -- auth-bootstrap-admin
    $firstExit=$LASTEXITCODE
    Expect 'Bootstrap first CLI exit' $firstExit 0
    $afterFirst=Snapshot
    $admins=@($afterFirst.valueRanges[0].values | Where-Object { $_[1] -eq 'admin' })
    Expect 'Exactly one bootstrap admin' $admins.Count 1
    $admin=$admins[0]
    Expect 'Admin must change password' ([bool]::Parse($admin[6])) $true
    $assignment=@($afterFirst.valueRanges[1].values | Where-Object { $_[1] -eq $admin[0] -and $_[2] -eq 'Admin' })
    Expect 'One Admin assignment' $assignment.Count 1
    $scope=@($afterFirst.valueRanges[2].values | Where-Object { $_[1] -eq $assignment[0][0] -and $_[3] -eq 'ALL' })
    Expect 'Three explicit ALL grants' $scope.Count 3
    $currentCfg=Get-Content -LiteralPath $secretsFile -Raw | ConvertFrom-Json
    Expect 'Bootstrap secret removed after commit' ([string]::IsNullOrWhiteSpace($currentCfg.'Auth:Bootstrap:AdminPassword')) $true
    # A different random one-use secret makes the no-reset check meaningful.
    $noopSecret=[Guid]::NewGuid().ToString('N')
    $currentCfg | Add-Member -NotePropertyName 'Auth:Bootstrap:AdminPassword' -NotePropertyValue $noopSecret -Force
    $temporarySecretFile=$secretsFile + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($temporarySecretFile,($currentCfg | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
        [IO.File]::Replace($temporarySecretFile,$secretsFile,[System.Management.Automation.Language.NullString]::Value)
    } finally {
        if ([IO.File]::Exists($temporarySecretFile)) { [IO.File]::Delete($temporarySecretFile) }
    }
    $currentCfg=$null; $noopSecret=$null
    & dotnet run --project $project --no-build --launch-profile https -- auth-bootstrap-admin
    Expect 'Bootstrap second CLI exit' $LASTEXITCODE 0
    $afterSecond=Snapshot
    # Full row comparison remains private: includes password hash/version/role/scope.
    $same=($afterFirst.valueRanges | ConvertTo-Json -Depth 12 -Compress) -ceq ($afterSecond.valueRanges | ConvertTo-Json -Depth 12 -Compress)
    Expect 'No-op preserves users/roles/scopes/versions' $same $true
    $currentCfg=Get-Content -LiteralPath $secretsFile -Raw | ConvertFrom-Json
    Expect 'Bootstrap secret removed after no-op' ([string]::IsNullOrWhiteSpace($currentCfg.'Auth:Bootstrap:AdminPassword')) $true
    $afterFirst=$null; $afterSecond=$null; $admin=$null; $scope=$null; $assignment=$null
    Write-Output ('PASS: ' + $assertions + ' bootstrap/no-op assertions; start web separately for manual Razor UAT.')
} catch {
    Write-Output ('FAIL: ' + $_.Exception.GetType().Name + '; no password/token/hash/response body printed.')
    exit 1
} finally {
    if ($googleClient) { $googleClient.Dispose() }
    $cfg=$null; $currentCfg=$null; $bootstrapPassword=$null; $grant=$null; $form=$null
}
