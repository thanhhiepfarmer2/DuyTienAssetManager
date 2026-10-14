# Read-only DEV verification and optional encrypted pre-write backup. No tokens/rows in output.
param([switch]$Backup)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.Security
$xml=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '../src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj') -Raw)
$store=Join-Path ([Environment]::GetFolderPath('ApplicationData')) ('Microsoft\UserSecrets\'+$xml.Project.PropertyGroup.UserSecretsId+'\secrets.json')
$client=[Net.Http.HttpClient]::new()
$client.Timeout=[TimeSpan]::FromSeconds(60)
try {
 $cfg=Get-Content -LiteralPath $store -Raw | ConvertFrom-Json
 function Cfg([string]$key) { $external=[Environment]::GetEnvironmentVariable($key.Replace(':','__')); if ($external) { return $external }; return $cfg.$key }
 foreach ($key in @('OAuthClientId','OAuthClientSecret','OAuthRefreshToken','SpreadsheetId','SystemSpreadsheetId','DriveFolderId')) { if (-not (Cfg ('GoogleWorkspace:'+$key))) { throw 'Missing configuration' } }
 foreach ($key in @('SheetsApiBaseUrl','DriveApiBaseUrl','OAuthTokenEndpoint')) { if (Cfg ('GoogleWorkspace:'+$key)) { throw 'Endpoint override requires review' } }
 $form=[Collections.Generic.Dictionary[string,string]]::new()
 $form.Add('client_id',(Cfg 'GoogleWorkspace:OAuthClientId')); $form.Add('client_secret',(Cfg 'GoogleWorkspace:OAuthClientSecret')); $form.Add('refresh_token',(Cfg 'GoogleWorkspace:OAuthRefreshToken')); $form.Add('grant_type','refresh_token')
 $response=$client.PostAsync('https://oauth2.googleapis.com/token',[Net.Http.FormUrlEncodedContent]::new($form)).GetAwaiter().GetResult()
 try { if (-not $response.IsSuccessStatusCode) { throw 'OAuth rejected' }; $grant=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json } finally { $response.Dispose() }
 if ($grant.scope -ne 'https://www.googleapis.com/auth/drive.file') { throw 'Scope rejected' }
 $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$grant.access_token)
 function GetJson([string]$uri) { $r=$client.GetAsync($uri).GetAwaiter().GetResult(); try { if (-not $r.IsSuccessStatusCode) { throw 'Google request rejected' }; return ($r.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json) } finally { $r.Dispose() } }
 $ids=@((Cfg 'GoogleWorkspace:SpreadsheetId'),(Cfg 'GoogleWorkspace:SystemSpreadsheetId'),(Cfg 'GoogleWorkspace:DriveFolderId'))
 if (@($ids | Select-Object -Unique).Count -ne 3) { throw 'Resources not distinct' }
 $names=@('DuyTien Assets DEV','DuyTien Asset Management - DEV System','DuyTien Assets DEV Files')
 $payload=[ordered]@{format='DUYTIEN_DEV_BACKUP_V1';createdAt=[DateTimeOffset]::UtcNow.ToString('O');sheets=@();driveManifest=@()}
 for ($i=0;$i -lt 3;$i++) {
  $file=GetJson ('https://www.googleapis.com/drive/v3/files/'+[uri]::EscapeDataString($ids[$i])+'?fields=name,mimeType,ownedByMe,trashed,capabilities(canEdit),permissions(type,role),shared')
  $acl=@($file.permissions); $type=if ($i -eq 2) {'application/vnd.google-apps.folder'} else {'application/vnd.google-apps.spreadsheet'}
  $valid=$file.name -eq $names[$i] -and $file.mimeType -eq $type -and $file.ownedByMe -and -not $file.trashed -and $file.capabilities.canEdit -and -not $file.shared -and $acl.Count -eq 1 -and $acl[0].type -eq 'user' -and $acl[0].role -eq 'owner'
  [pscustomobject]@{Resource=$names[$i];VerifiedDev=$valid;OwnerOnly=($acl.Count -eq 1);ExactDriveFile=$true} | ConvertTo-Json -Compress
  if (-not $valid) { [pscustomobject]@{NameMatches=($file.name -eq $names[$i]);NameHasDevLabel=($file.name -match '(?i)dev');MimeMatches=($file.mimeType -eq $type);OwnedByMe=$file.ownedByMe;CanEdit=$file.capabilities.canEdit;Trashed=$file.trashed;Shared=$file.shared;PermissionCount=$acl.Count;PermissionTypes=@($acl | ForEach-Object {$_.type});PermissionRoles=@($acl | ForEach-Object {$_.role});DevLabel= $(if ($file.name -match '(?i)dev') {$file.name} else {'[non-DEV name hidden]'})} | ConvertTo-Json -Compress; throw 'Unexpected DEV resource/ACL' }
  if ($i -lt 2) {
   $base='https://sheets.googleapis.com/v4/spreadsheets/'+[uri]::EscapeDataString($ids[$i])
   $meta=GetJson ($base+'?fields=sheets.properties(sheetId,title,gridProperties)')
   $titles=@($meta.sheets | ForEach-Object {$_.properties.title})
   [pscustomobject]@{Resource=$names[$i];TabCount=$titles.Count;Titles=$titles} | ConvertTo-Json -Compress
   if ($Backup) {
    $ranges=@($meta.sheets | ForEach-Object {'ranges='+[uri]::EscapeDataString("'"+$_.properties.title.Replace("'","''")+"'")})
    $values=GetJson ($base+'/values:batchGet?'+($ranges -join '&')+'&valueRenderOption=FORMULA&dateTimeRenderOption=SERIAL_NUMBER')
    $payload.sheets+=@{resourceId=$ids[$i];metadata=$meta;values=$values}
   }
  }
 }
 if ($Backup) {
  $next=$null
  do {
   $query=[uri]::EscapeDataString("'"+$ids[2]+"' in parents and trashed = false")
   $uri='https://www.googleapis.com/drive/v3/files?q='+$query+'&pageSize=1000&fields=nextPageToken,files(id,name,mimeType,size,md5Checksum,parents,appProperties)'
   if ($next) { $uri+='&pageToken='+[uri]::EscapeDataString($next) }
   $page=GetJson $uri; $payload.driveManifest+=@($page.files); $next=$page.nextPageToken
  } while ($next)
  $destination=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DuyTienAssets\Task014Backup'
  [IO.Directory]::CreateDirectory($destination) | Out-Null
  $json=$payload | ConvertTo-Json -Depth 100 -Compress
  $bytes=[Text.Encoding]::UTF8.GetBytes($json)
  $sealed=[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
  $path=Join-Path $destination ('dev-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N')+'.dpapi')
  [IO.File]::WriteAllBytes($path,$sealed)
  $roundTrip=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($path),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
  if ([Convert]::ToBase64String($bytes) -ne [Convert]::ToBase64String($roundTrip)) { throw 'Backup verification failed' }
  [pscustomobject]@{BackupEncrypted=$true;DecryptRoundTrip=$true;OutsideGit=$true;SheetResources=$payload.sheets.Count;DriveManifestOnly=$true;Path=$path;RestoreRun=$false} | ConvertTo-Json -Compress
  [Array]::Clear($bytes,0,$bytes.Length); [Array]::Clear($roundTrip,0,$roundTrip.Length)
 }
} catch { Write-Output ('FAIL: '+$_.Exception.GetType().Name+'; no secret/resource IDs/body printed'); exit 1 }
finally { $client.Dispose(); $cfg=$null; $grant=$null; $form=$null; $payload=$null; $json=$null }
