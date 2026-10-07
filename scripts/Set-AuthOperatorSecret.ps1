param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Bootstrap", "Recovery")]
    [string]$Purpose,
    [string]$Project = "src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj"
)

$ErrorActionPreference = "Stop"
$key = "Auth:${Purpose}:AdminPassword"
$secureValue = Read-Host -Prompt "Nhập mật khẩu $Purpose admin (không hiển thị)" -AsSecureString
$valuePointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
try {
    $plainValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($valuePointer)
    if ([string]::IsNullOrEmpty($plainValue)) {
        throw "Mật khẩu không được để trống."
    }

    & dotnet user-secrets set $key $plainValue --project $Project
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet user-secrets failed for key '$key'."
    }
}
finally {
    if ($valuePointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($valuePointer)
    }

    $plainValue = $null
    $secureValue.Dispose()
}

Write-Host "Đã lưu secret $Purpose vào user-secrets mà không in giá trị. Lệnh auth sẽ xóa khóa local sau khi thành công."
