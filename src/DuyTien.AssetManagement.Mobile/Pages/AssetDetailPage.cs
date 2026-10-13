using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Mobile.Client;
using DuyTien.AssetManagement.Mobile.Services;
namespace DuyTien.AssetManagement.Mobile.Pages;

public sealed class AssetDetailPage : MobilePage
{
    private AssetView view;
    private readonly MobileIdentity identity;
    private readonly Label info = new() { LineBreakMode = LineBreakMode.WordWrap };
    public AssetDetailPage(MobileRuntime runtime, MobileIdentity identity, AssetView view) : base(runtime, "Chi tiết tài sản")
    {
        this.view = view; this.identity = identity; Body.Add(info); Render();
        Action("Tải lại chi tiết", Load);
        if (identity.Roles.Any(x => x is "Admin" or "AssetEditor"))
            Action("Sửa tài sản demo", async () =>
            {
                var fresh = await Runtime.Assets.DetailAsync(this.view.Asset.AssetId);
                var choices = await Runtime.Assets.ChoicesAsync(true);
                await Navigation.PushAsync(new AssetEditPage(Runtime, choices, fresh)); Status.Text = "";
            });
        Action("Ảnh tài sản", async () =>
        {
            var fresh = await Runtime.Assets.DetailAsync(this.view.Asset.AssetId);
            await Navigation.PushAsync(new PhotoPage(Runtime, fresh.Asset, this.identity)); Status.Text = "";
        });
    }
    protected override async void OnAppearing() { base.OnAppearing(); await RunAsync(Load); }
    private async Task Load()
    {
        info.Text = "";
        view = await Runtime.Assets.DetailAsync(view.Asset.AssetId); Render(); Status.Text = "Đã kiểm tra lại dữ liệu/quyền.";
    }
    private void Render()
    {
        var a = view.Asset;
        info.Text = $"{a.ObjectCode} — {a.Name}\nID: {a.AssetId} / version{a.Version}\n{a.DataState}; danh mục {view.CategoryState}\n{view.CategoryName} ({a.CategoryId})\n{view.OrgUnitName} ({a.OrgUnitId})\n{view.LocationName} ({a.LocationId})\nLoại: {a.Type}; tình trạng: {a.Condition}\nNgười giữ: {a.CustodianRef}\nSerial: {a.Serial}; model: {a.Model}; hãng: {a.Manufacturer}\nNgày ghi nhận: {a.RecordedOn?.ToString("yyyy-MM-dd") ?? "chưa có"}\nAlias: {string.Join(", ", view.Aliases.Where(x => x.IsActive).Select(x => x.OldCode))}\nGhi chú: {a.Notes}\nCập nhật: {a.UpdatedAt:O}\nNguồn/rule công ty OPEN/TBD; không là dữ liệu đã duyệt.";
    }
}
