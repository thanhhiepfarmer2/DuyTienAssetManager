# Temporary operator credential for TASK-005 dev UAT; no application auth policy change.
$ErrorActionPreference='Stop'
$key='UatTask005:AdminPassword'
$project=Join-Path $PSScriptRoot '..\src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj'
$xml=[xml](Get-Content -LiteralPath $project -Raw)
$store=Join-Path ([Environment]::GetFolderPath('ApplicationData')) ('Microsoft\UserSecrets\' + $xml.Project.PropertyGroup.UserSecretsId + '\secrets.json')
$secure=Read-Host -Prompt 'Nhập mật khẩu admin hiện tại cho UAT (ẩn, không gửi vào chat)' -AsSecureString
$pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$temporary=$store + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    $value=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ($value.Length -lt 8) { throw 'Mật khẩu phải có ít nhất 8 ký tự.' }
    $cfg=Get-Content -LiteralPath $store -Raw | ConvertFrom-Json
    $cfg | Add-Member -NotePropertyName $key -NotePropertyValue $value -Force
    [IO.File]::WriteAllText($temporary,($cfg | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    [IO.File]::Replace(
    $temporary,
    $store,
    [System.Management.Automation.Language.NullString]::Value
    )
    Write-Output 'Đã lưu credential UAT tạm ngoài repo; runner xóa khóa khi kết thúc. Không in giá trị.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    $secure.Dispose(); $value=$null; $cfg=$null
}
