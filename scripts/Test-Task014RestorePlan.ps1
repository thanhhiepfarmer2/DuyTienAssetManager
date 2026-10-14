# Inspect encrypted DEV backup and prepare an isolated restore plan. NO Google writes.
param([Parameter(Mandatory=$true)][string]$BackupPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
try {
 $allowed=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DuyTienAssets\Task014Backup'))+[IO.Path]::DirectorySeparatorChar
 $actual=[IO.Path]::GetFullPath($BackupPath)
 if (-not $actual.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($actual) -ne '.dpapi') { throw 'Only this task encrypted private backups are accepted' }
 $bytes=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($actual),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
 $backup=[Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
 if ($backup.format -ne 'DUYTIEN_DEV_BACKUP_V1' -or @($backup.sheets).Count -ne 2) { throw 'Unsupported backup format' }
 $tableCount=0; $hasAuth=$false; $hasRoles=$false; $hasScopes=$false
 foreach ($resource in $backup.sheets) {
  $tables=@($resource.metadata.sheets)
  if ($tables.Count -eq 0 -or @($tables | ForEach-Object {$_.properties.sheetId} | Select-Object -Unique).Count -ne $tables.Count -or @($tables | ForEach-Object {$_.properties.title} | Select-Object -Unique).Count -ne $tables.Count) { throw 'Duplicate/missing metadata' }
  $ranges=@($resource.values.valueRanges)
  if ($ranges.Count -ne $tables.Count) { throw 'Missing value ranges' }
  foreach ($table in $tables) {
   $title=$table.properties.title
   $range=@($ranges | Where-Object { $_.range.StartsWith("'"+$title+"'!") -or $_.range.StartsWith($title+'!') })
   if ($range.Count -ne 1) { throw 'Backup range mismatch' }
  }
  $titles=@($tables | ForEach-Object {$_.properties.title})
  $hasAuth=$hasAuth -or 'Users' -in $titles; $hasRoles=$hasRoles -or 'UserRoleAssignments' -in $titles; $hasScopes=$hasScopes -or 'RoleAssignmentScopes' -in $titles
  $tableCount+=$tables.Count
 }
 [pscustomobject]@{PlanValidated=$true;EncryptedInput=$true;GoogleWrites=0;NewSeparateDevTargetsRequired=$true;OverwriteCurrentResourcesAllowed=$false;RequirePreserveLogicalIds=$true;AuthTablesIncluded=$hasAuth;RolesIncluded=$hasRoles;ScopesIncluded=$hasScopes;SheetTableCount=$tableCount;DriveManifestOnly=$true;DriveBytesBackupComplete=$false;ActualRestoreRun=$false} | ConvertTo-Json -Compress
 # Execution is intentionally absent: create two new DEV restore resources, verify owner-only ACL,
 # restore typed cell data and validate adapters/ledger/IDs/links there before any switch.
 # Formula/text typing, active-session handling and Drive byte retention must be resolved first.
} catch { Write-Output ('FAIL: '+$_.Exception.GetType().Name+'; backup contents/credential values hidden'); exit 1 }
finally { if ($bytes) { [Array]::Clear($bytes,0,$bytes.Length) }; $backup=$null }
