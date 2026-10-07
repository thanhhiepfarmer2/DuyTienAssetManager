param(
    [string]$Project = "src\DuyTien.AssetManagement.Server\DuyTien.AssetManagement.Server.csproj"
)

$ErrorActionPreference = "Stop"

$settings = @(
    @{ Key = "GoogleWorkspace:OAuthClientId"; Prompt = "OAuth client ID" },
    @{ Key = "GoogleWorkspace:OAuthClientSecret"; Prompt = "OAuth client secret" },
    @{ Key = "GoogleWorkspace:OAuthRefreshToken"; Prompt = "OAuth refresh token (để trống nếu sẽ chạy bootstrap)" },
    @{ Key = "GoogleWorkspace:SpreadsheetId"; Prompt = "Spreadsheet ID dev (ID không thay thế quyền Picker/provisioning)" },
    @{ Key = "GoogleWorkspace:SystemSpreadsheetId"; Prompt = "System spreadsheet ID dev (ưu tiên để lệnh provisioning/Picker tự lưu)" },
    @{ Key = "GoogleWorkspace:DriveFolderId"; Prompt = "Drive folder ID dev (ID không thay thế quyền Picker/provisioning)" },
    @{ Key = "GoogleWorkspace:AccessDeniedProbeFileId"; Prompt = "ID file control chưa cấp quyền (chỉ dùng live access-boundary test)" }
)

foreach ($setting in $settings) {
    $secureValue = Read-Host -Prompt $setting.Prompt -AsSecureString
    $valuePointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try {
        $plainValue = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($valuePointer)
        if (-not [string]::IsNullOrWhiteSpace($plainValue)) {
            & dotnet user-secrets set $setting.Key $plainValue --project $Project
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet user-secrets failed for key '$($setting.Key)'."
            }
        }
    }
    finally {
        if ($valuePointer -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($valuePointer)
        }

        $plainValue = $null
        $secureValue.Dispose()
    }
}

Write-Host "Đã cập nhật các giá trị không để trống vào user-secrets; không in giá trị ra màn hình."
