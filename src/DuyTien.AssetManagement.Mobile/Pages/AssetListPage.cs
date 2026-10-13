using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Mobile.Client;
using DuyTien.AssetManagement.Mobile.Services;
namespace DuyTien.AssetManagement.Mobile.Pages;

public sealed class AssetListPage : MobilePage
{
    private readonly Entry q, category, org, location, condition, custodian;
    private readonly CheckBox descendants = new() { IsChecked = true };
    private readonly VerticalStackLayout results = new() { Spacing = 10 };
    private readonly Label summary = new();
    private readonly MobileIdentity identity;
    private AssetQuery query = new();
    private AssetList? list;
    public AssetListPage(MobileRuntime runtime, MobileIdentity identity) : base(runtime, "Tài sản theo phạm vi")
    {
        this.identity = identity;
        Body.Add(new Label { Text = "DRAFT_DEMO — chỉ từng trang dữ liệu được server cho phép." });
        q = Field("Tìm mã/tên/serial/alias"); category = Field("Lọc ID danh mục (tùy chọn)");
        Body.Add(new HorizontalStackLayout { Children = { descendants, new Label { Text = "Bao gồm danh mục con", VerticalOptions = LayoutOptions.Center } } });
        org = Field("Lọc ID đơn vị (tùy chọn)"); location = Field("Lọc ID địa điểm (tùy chọn)");
        condition = Field("Lọc tình trạng (tùy chọn, không có rule công ty mặc định)"); custodian = Field("Lọc người giữ — tham chiếu (tùy chọn)");
        Action("Tìm / áp dụng lọc", async () =>
        {
            query = new(Q: Blank(q), CategoryId: Blank(category), IncludeDescendants: descendants.IsChecked,
                Condition: Blank(condition), OrgUnitId: Blank(org), LocationId: Blank(location), CustodianRef: Blank(custodian));
            await Load();
        });
        if (identity.Roles.Any(x => x is "Admin" or "AssetEditor"))
            Action("Tạo tài sản demo", async () =>
            {
                var choices = await Runtime.Assets.ChoicesAsync(false);
                await Navigation.PushAsync(new AssetEditPage(Runtime, choices)); Status.Text = "";
            });
        Body.Add(summary); Body.Add(results);
        Action("Trang trước", async () => { if (query.Page > 1) { query = query with { Page = query.Page - 1 }; await Load(); } else Status.Text = "Đang ở trang đầu."; });
        Action("Trang sau", async () => { if (list is not null && query.Page * query.PageSize < list.Total) { query = query with { Page = query.Page + 1 }; await Load(); } else Status.Text = "Không có trang tiếp theo."; });
        Action("Tải lại trang", Load);
    }
    protected override async void OnAppearing() { base.OnAppearing(); await RunAsync(Load); }
    private static string? Blank(Entry e) => string.IsNullOrWhiteSpace(e.Text) ? null : e.Text.Trim();
    private async Task Load()
    {
        // Clear previously displayed data on a failed recheck; do not present it as current scope.
        results.Clear(); summary.Text = ""; list = null;
        list = await Runtime.Assets.ListAsync(query);
        summary.Text = $"{list.Total} tài sản trong phạm vi; trang{list.Page}; {list.SnapshotAsOf:O}";
        foreach (var row in list.Items)
        {
            var asset = row.Asset;
            var button = new Button { Text = asset.ObjectCode + " — " + asset.Name + " [" + asset.DataState + "]", MinimumHeightRequest = 48 };
            button.Clicked += async (_, _) => await RunAsync(async () =>
            { var detail = await Runtime.Assets.DetailAsync(asset.AssetId); await Navigation.PushAsync(new AssetDetailPage(Runtime, identity, detail)); Status.Text = ""; });
            results.Add(button);
        }
        Status.Text = list.Items.Count == 0 ? "Không có tài sản ở trang/bộ lọc này. Total chỉ tính phạm vi hiện tại." : "Đã tải trang theo quyền hiện tại.";
    }
}
