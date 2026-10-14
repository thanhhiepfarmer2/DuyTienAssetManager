# Compare preservation without emitting auth rows/hashes/tokens or any Google request.
param([Parameter(Mandatory=$true)][string]$BeforePath,[Parameter(Mandatory=$true)][string]$AfterPath)
$ErrorActionPreference='Stop';Add-Type -AssemblyName System.Security
function ReadBackup([string]$path) {
 $base=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DuyTienAssets\Task014Backup'))+[IO.Path]::DirectorySeparatorChar
 $full=[IO.Path]::GetFullPath($path);if (-not $full.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)) { throw 'Private backup path required' }
 $raw=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($full),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 try { return ([Text.Encoding]::UTF8.GetString($raw) | ConvertFrom-Json) } finally { [Array]::Clear($raw,0,$raw.Length) }
}
function Rows($backup,[string]$title) {
 $found=@($backup.sheets | ForEach-Object {$_.values.valueRanges} | Where-Object {$_.range.StartsWith("'"+$title+"'!") -or $_.range.StartsWith($title+'!')})
 if ($found.Count -ne 1) { throw 'Missing/duplicate backup table' };return ,@($found[0].values)
}
try {
 $before=ReadBackup $BeforePath;$after=ReadBackup $AfterPath
 $pocSame=(ConvertTo-Json -InputObject (Rows $before 'TASK004_POC') -Depth 100 -Compress) -eq (ConvertTo-Json -InputObject (Rows $after 'TASK004_POC') -Depth 100 -Compress)
 $u0=Rows $before 'Users';$u1=Rows $after 'Users';$headers=@($u0[0]);$selected=@(0..($headers.Count-1) | Where-Object {$headers[$_] -match '(?i)(Password|SecurityStamp|AuthVersion|State|UserId|Username|DisplayName|CreatedAt|MustChange)'})
 $usersSame=$true
 foreach ($row in @($u0 | Select-Object -Skip 1)) {
  $new=@($u1 | Select-Object -Skip 1 | Where-Object {$_[0] -eq $row[0]});if ($new.Count -ne 1) {$usersSame=$false;break}
  foreach ($i in $selected) {if ([string]$row[$i] -cne [string]$new[0][$i]) {$usersSame=$false}}
 }
 $rolesSame=$true
 foreach ($title in @('Roles','Permissions','RolePermissions','Positions','PositionRoleTemplates','UserRoleAssignments','RoleAssignmentScopes')) {
  $a=Rows $before $title;$b=Rows $after $title
  $set=@{};foreach ($row in $b) {$set[(ConvertTo-Json -InputObject $row -Depth 100 -Compress)]=$true}
  foreach ($row in $a) {if (-not $set.ContainsKey((ConvertTo-Json -InputObject $row -Depth 100 -Compress))) {$rolesSame=$false}}
 }
 [pscustomobject]@{PocRowsUnchanged=$pocSame;ExistingUserCredentialStatePreserved=$usersSame;ExistingRolesAssignmentsScopesPreserved=$rolesSame;OriginalUserCount=($u0.Count-1);CurrentUserCount=($u1.Count-1);Source='Local encrypted backup comparison';GoogleWrites=0;SensitiveValuesPrinted=$false} | ConvertTo-Json -Compress
 if (-not ($pocSame -and $usersSame -and $rolesSame)) {throw 'Preservation comparison failed'}
} catch {Write-Output ('FAIL: '+$_.Exception.GetType().Name+'; values hidden');exit 1}
finally {$before=$null;$after=$null}
