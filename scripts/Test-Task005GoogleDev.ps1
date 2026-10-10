# Chi doc system spreadsheet DEV. Khong in secret/token/ID/email/hash/body.
# Khong init/bootstrap/ghi Google; khong thay grant provisioning/Picker.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$projectFile = Join-Path $PSScriptRoot "..\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj"
$projectXml = [xml](Get-Content -LiteralPath $projectFile -Raw)
$secretId = $projectXml.Project.PropertyGroup.UserSecretsId
$secretsFile = Join-Path ([Environment]::GetFolderPath('ApplicationData')) ("Microsoft\UserSecrets\" + $secretId + "\secrets.json")
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(30)
try {
    $cfg = Get-Content -LiteralPath $secretsFile -Raw | ConvertFrom-Json
    function ConfigValue([string]$key) {
        $external = [Environment]::GetEnvironmentVariable($key.Replace(':','__'))
        if (-not [string]::IsNullOrWhiteSpace($external)) { return $external }
        return $cfg.$key
    }
    $id = ConfigValue 'GoogleWorkspace:SystemSpreadsheetId'
    if ([string]::IsNullOrWhiteSpace($id)) { throw 'Missing system spreadsheet configuration.' }
    $form = [Collections.Generic.Dictionary[string,string]]::new()
    $form.Add('client_id', (ConfigValue 'GoogleWorkspace:OAuthClientId'))
    $form.Add('client_secret', (ConfigValue 'GoogleWorkspace:OAuthClientSecret'))
    $form.Add('refresh_token', (ConfigValue 'GoogleWorkspace:OAuthRefreshToken'))
    $form.Add('grant_type', 'refresh_token')
    $response = $client.PostAsync('https://oauth2.googleapis.com/token', [System.Net.Http.FormUrlEncodedContent]::new($form)).GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw ('OAuth HTTP ' + [int]$response.StatusCode) }
    $grant = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    if ($grant.scope -ne 'https://www.googleapis.com/auth/drive.file') { throw 'OAuth scope rejected.' }
    Write-Output 'OAuthExactDriveFile=True'
    $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $grant.access_token)
    function GetJson([string]$uri) {
        $r = $client.GetAsync($uri).GetAwaiter().GetResult()
        try {
            if (-not $r.IsSuccessStatusCode) { throw ('Google HTTP ' + [int]$r.StatusCode) }
            return ($r.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
        } finally { $r.Dispose() }
    }
    $file = GetJson ('https://www.googleapis.com/drive/v3/files/' + [uri]::EscapeDataString($id) + '?fields=name,mimeType,ownedByMe,trashed,capabilities(canEdit),permissions(type,role),shared')
    $acl = @($file.permissions)
    $restricted = $acl.Count -eq 1 -and $acl[0].type -eq 'user' -and $acl[0].role -eq 'owner' -and -not $file.shared
    $separate = $id -ne (ConfigValue 'GoogleWorkspace:SpreadsheetId')
    $valid = $file.name -eq 'DuyTien Asset Management - DEV System' -and $file.mimeType -eq 'application/vnd.google-apps.spreadsheet' -and $file.ownedByMe -and -not $file.trashed -and $file.capabilities.canEdit -and $restricted -and $separate
    [pscustomobject]@{ResourceName=$file.name;OwnedByAuthenticatedOperator=$file.ownedByMe;AclOwnerOnly=$restricted;PermissionCount=$acl.Count;DistinctFromBusiness=$separate;DevResourceVerified=$valid} | ConvertTo-Json -Compress
    if (-not $valid) { throw 'Unexpected DEV resource or ACL; no writes allowed.' }
    $sheetUrl = 'https://sheets.googleapis.com/v4/spreadsheets/' + [uri]::EscapeDataString($id)
    $metadata = GetJson ($sheetUrl + '?fields=sheets.properties(title)')
    $required = @('SystemSchemaVersions','Users','Roles','Permissions','RolePermissions','Positions','PositionRoleTemplates','UserRoleAssignments','RoleAssignmentScopes','Sessions','RefreshTokens','SecurityEvents','AuthOperations')
    $titles = @($metadata.sheets | ForEach-Object { $_.properties.title })
    $missing = @($required | Where-Object { $_ -notin $titles })
    Write-Output ('RequiredTabCount=' + $required.Count + '; MissingTabCount=' + $missing.Count)
    if ($missing.Count -gt 0) { throw 'System schema tabs missing.' }
    $version = GetJson ($sheetUrl + '/values/' + [uri]::EscapeDataString("'SystemSchemaVersions'!A2:A2"))
    $schemaVersion = [string]$version.values[0][0]
    Write-Output ('SchemaVersion=' + $schemaVersion)
    if ($schemaVersion -ne '1') { throw 'Unexpected schema version.' }
    $roles = GetJson ($sheetUrl + '/values/' + [uri]::EscapeDataString("'Roles'!B2:B"))
    $roleCodes = @($roles.values | ForEach-Object { $_[0] })
    $catalogMatches = (@($roleCodes | Sort-Object) -join ',') -eq 'Admin,AssetEditor,CatalogManager,DocumentManager,Viewer'
    Write-Output ('RoleCount=' + $roleCodes.Count + '; CatalogMatches=' + $catalogMatches)
    if (-not $catalogMatches) { throw 'Unexpected role catalog.' }
    $users = GetJson ($sheetUrl + '/values/' + [uri]::EscapeDataString("'Users'!B2:B"))
    $usernames = @($users.values | Where-Object { $null -ne $_ } | ForEach-Object { $_[0] })
    Write-Output ('UserRecordCount=' + $usernames.Count + '; AdminExists=' + ('admin' -in $usernames))
    Write-Output 'PASS: DEV resource, ACL, scope and initialized schema verified on Google.'
} catch {
    Write-Output ('FAIL: ' + $_.Exception.GetType().Name + '; no credentials, resource ID, email or Google response body printed.')
    exit 1
} finally {
    $client.Dispose()
    $cfg = $null; $grant = $null; $form = $null; $file = $null; $users = $null
}
