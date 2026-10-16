using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
namespace DuyTien.AssetManagement.Server.Presentation;

public sealed record WebNavigation(string Url, string Label, string Icon);
public static class WebUi
{
    public const string CacheRevision = "web-ui-2026-10-16.3";
    public static IReadOnlyList<WebNavigation> Navigation(ClaimsPrincipal user)
    {
        var items = new List<WebNavigation> { new("/", "Trang chủ", "home") };
        if (user.Identity?.IsAuthenticated == true)
        {
            items.AddRange([new("/Categories", "Danh mục", "tree"), new("/Assets", "Tài sản", "asset"), new("/Documents", "Hồ sơ và biểu mẫu", "file"), new("/Items", "Vật tư, hàng hóa", "items"), new("/Stock", "Số lượng", "stock"), new("/Reports", "Báo cáo", "report")]);
            if (user.IsInRole("Admin")) items.AddRange([new("/Admin/Users", "Tài khoản", "users"), new("/Admin/Authorization", "Phân quyền", "shield"), new("/Admin/SecurityEvents", "Nhật ký bảo mật", "history")]);
        }
        items.Add(new("/Help", "Hướng dẫn", "help")); return items;
    }
    public static bool Active(string path, string url) => url == "/" ? path == "/" : path.Equals(url, StringComparison.OrdinalIgnoreCase) || path.StartsWith(url + "/", StringComparison.OrdinalIgnoreCase);
    public static WebNavigation? Module(string path) => new[] { new WebNavigation("/Categories", "Danh mục", "tree"), new("/Assets", "Tài sản", "asset"), new("/Documents", "Hồ sơ và biểu mẫu", "file"), new("/Items", "Vật tư, hàng hóa", "items"), new("/Stock", "Số lượng", "stock"), new("/Reports", "Báo cáo", "report"), new("/Admin/Users", "Tài khoản", "users"), new("/Admin/Authorization", "Phân quyền", "shield"), new("/Admin/SecurityEvents", "Nhật ký bảo mật", "history"), new("/Help", "Hướng dẫn", "help") }.FirstOrDefault(x => Active(path, x.Url));
    public static string Scope(string type) => type switch { "OrgUnit" => "Đơn vị", "Location" => "Địa điểm", "CategorySubtree" => "Danh mục", _ => type };
    public static string State(string? state) => state switch
    {
        "ADD" => "Thêm", "UPDATE" => "Cập nhật", "SKIP" => "Giữ nguyên", "QUARANTINE" => "Cách ly", "DRAFT" => "Chưa duyệt", "DRAFT_DEMO" => "Dữ liệu kiểm thử", "PUBLISHED_DEMO" => "Đã phát hành · dữ liệu kiểm thử", "STAGING" => "Đang chuẩn bị", "VALIDATED" => "Đã kiểm tra, chờ lưu", "PARTIAL" => "Đang nhập từng phần", "COMPLETE" => "Hoàn tất", "PUBLISHED" => "Đã phát hành", "ARCHIVED" => "Đã lưu trữ", "Active" or "ACTIVE" => "Đang hoạt động", "Locked" or "LOCKED" => "Đã khóa", "Revoked" or "REVOKED" => "Đã thu hồi", "PENDING" => "Đang xử lý", "AVAILABLE" => "Sẵn sàng", "FAILED" => "Không thành công", "OUT" => "Đã hết", "LOW" => "Sắp hết", "UNKNOWN" => "Chưa có số lượng", "NOT_TRACKED" => "Không theo dõi tồn", "TRACKING_UNCONFIRMED" => "Chưa xác nhận theo dõi tồn", "MISSING_MINIMUM" => "Chưa có ngưỡng", "SOURCE_MISSING" => "Thiếu nguồn", "INVALID_SNAPSHOT" => "Thông tin số lượng chưa hợp lệ", "Success" => "Thành công", "Failure" => "Không thành công", "Admin" => "Quản trị viên", "Viewer" => "Người tra cứu", "AssetEditor" => "Quản lý tài sản", "CatalogManager" => "Quản lý danh mục", "DocumentManager" => "Quản lý hồ sơ", _ => state ?? "Chưa có thông tin"
    };
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["Type"]="Loại tài sản", ["Serial"]="Số sê-ri", ["Model"]="Model", ["Manufacturer"]="Hãng sản xuất", ["Name"]="Tên", ["BusinessCode"]="Mã nghiệp vụ", ["CategoryId"]="Danh mục", ["OrgUnitId"]="Đơn vị", ["LocationId"]="Địa điểm", ["CustodianRef"]="Người quản lý", ["SerialNumber"]="Số sê-ri", ["RecordedOn"]="Ngày ghi nhận", ["Condition"]="Tình trạng", ["AliasesText"]="Mã tham chiếu", ["Notes"]="Ghi chú", ["Description"]="Mô tả", ["Upload"]="Chọn tệp", ["Label"]="Tên phiên bản", ["Kind"]="Loại", ["Reason"]="Lý do", ["Source"]="Nguồn", ["AsOf"]="Thời điểm số liệu", ["Quantity"]="Số lượng", ["Minimum"]="Ngưỡng tối thiểu", ["Unit"]="Đơn vị tính", ["ItemId"]="Vật tư, hàng hóa", ["Q"]="Tìm kiếm", ["ExpectedVersion"]="Phiên bản đang sửa", ["IsPrimary"]="Ảnh đại diện", ["EffectiveOn"]="Ngày hiệu lực", ["DisplayName"]="Tên hiển thị", ["RoleCode"]="Vai trò", ["IncludeDescendants"]="Bao gồm cấp con", ["ConfirmDemo"]="Xác nhận đây là dữ liệu kiểm thử", ["ConfirmImpact"]="Tôi đã kiểm tra ảnh hưởng", ["DestinationCategoryId"]="Danh mục đích", ["DestinationOrgUnitId"]="Đơn vị đích", ["DestinationLocationId"]="Địa điểm đích", ["SegmentCode"]="Mã đoạn", ["Definition"]="Định nghĩa", ["AssetIds"]="Tài sản được chọn"
    };
}
public sealed class WebUiMetadataProvider : IDisplayMetadataProvider
{
    public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
    { if (context.Key.Name is { } name && context.DisplayMetadata.DisplayName is null && WebUi.Labels.TryGetValue(name, out var label)) context.DisplayMetadata.DisplayName = () => label; }
}
