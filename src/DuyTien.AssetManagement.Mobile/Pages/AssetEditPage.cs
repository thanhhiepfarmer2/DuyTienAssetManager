using System.Globalization;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Mobile.Client;
using DuyTien.AssetManagement.Mobile.Services;
namespace DuyTien.AssetManagement.Mobile.Pages;

public sealed class AssetEditPage : MobilePage
{
    private readonly MobileAssetDraft draft;
    private readonly VerticalStackLayout fields = new() { Spacing = 8 };
    private readonly Entry name, custodian, serial, model, manufacturer, date, condition, aliases;
    private readonly Editor notes = new() { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 100 };
    private readonly Picker category = new() { Title = "Chọn danh mục DRAFT", ItemDisplayBinding = new Binding("Name") };
    private readonly Picker org = new() { Title = "Chọn đơn vị demo", ItemDisplayBinding = new Binding("Name") };
    private readonly Picker location = new() { Title = "Chọn địa điểm demo", ItemDisplayBinding = new Binding("Name") };
    public AssetEditPage(MobileRuntime runtime, AssetFormChoices choices, AssetView? existing = null) : base(runtime, existing is null ? "Tạo tài sản demo" : "Sửa tài sản demo")
    {
        var a = existing?.Asset;
        var input = a is null ? new AssetInput("", "", "", "") : new(a.Name, a.CategoryId, a.OrgUnitId, a.LocationId, a.Type, a.CustodianRef, a.Serial, a.Model, a.Manufacturer, a.RecordedOn, a.Condition, a.Notes, existing!.Aliases.Where(x => x.IsActive).Select(x => x.OldCode).ToArray());
        draft = new(runtime.Assets, a?.AssetId, input, a?.Version ?? 0, choices.TreeVersionId ?? "");
        Body.Add(new Label { Text = "DRAFT_DEMO / MACHINE. Required fields kỹ thuật theo backend; không tự chốt rule công ty. Mã/ID do server cấp và giữ nguyên." });
        name = Field("Tên tài sản *", input.Name);
        Pick("Danh mục DRAFT *", category, choices.Categories, input.CategoryId);
        Pick("Đơn vị demo *", org, choices.OrgUnits, input.OrgUnitId);
        Pick("Địa điểm demo *", location, choices.Locations, input.LocationId);
        custodian = Field("Tham chiếu người giữ (không tự tạo tài khoản)", input.CustodianRef);
        serial = Field("Serial", input.Serial); model = Field("Model", input.Model); manufacturer = Field("Hãng", input.Manufacturer);
        date = Field("Ngày ghi nhận yyyy-MM-dd (để trống nếu chưa biết)", input.RecordedOn?.ToString("yyyy-MM-dd") ?? "");
        condition = Field("Tình trạng *", input.Condition); aliases = Field("Alias (mỗi dòng/mã, phân cách dấu chấm phẩy)", string.Join(";", input.Aliases ?? []));
        Body.Add(new Label { Text = "Ghi chú" }); notes.Text = input.Notes; Body.Add(notes);
        // Retain exactly these controls while the first write is uncertain.
        foreach (var child in Body.Children.ToArray()) { Body.Remove(child); fields.Add(child); }
        Body.Add(fields);
        Action("Lưu / retry cùng yêu cầu", async () =>
        {
            draft.Input = ReadInput();
            try
            {
                var result = await draft.SaveAsync(); fields.IsEnabled = false;
                Status.Text = $"Đã lưu {result.Asset.ObjectCode}, version{result.Asset.Version}, {result.Asset.DataState}" + (result.Replayed ? " (replay, không tạo bản sao)." : ".");
            }
            finally { fields.IsEnabled = !draft.HasPending && draft.Saved is null; UpdateBackGuard(); }
        });
        Action("Sau409: nạp version mới, giữ nội dung để kiểm tra", async () =>
        {
            var currentCategory = (category.SelectedItem as AssetChoice)?.Id;
            var currentOrg = (org.SelectedItem as AssetChoice)?.Id; var currentLocation = (location.SelectedItem as AssetChoice)?.Id;
            var choicesNow = await draft.RebaseAsync();
            Rebind(category, choicesNow.Categories, currentCategory); Rebind(org, choicesNow.OrgUnits, currentOrg); Rebind(location, choicesNow.Locations, currentLocation);
            fields.IsEnabled = true; UpdateBackGuard();
            Status.Text = $"Đã nạp version{draft.Version}/cây hiện tại. Nội dung chưa lưu vẫn giữ; kiểm tra rồi bấm Lưu. Không tự ghi đè.";
        });
        Action("Đăng xuất / bỏ bản form RAM", async () => await Runtime.Session!.LogoutAsync());
        Action("Ảnh tài sản đã lưu", async () =>
        {
            if (draft.Saved is null) throw new MobileApiException(400, "Lưu tài sản thành công trước khi upload ảnh.");
            var me = await Runtime.Assets.MeAsync(); await Navigation.PushAsync(new PhotoPage(Runtime, draft.Saved, me)); Status.Text = "";
        });
    }
    private void UpdateBackGuard() => Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsEnabled = !draft.HasPending, IsVisible = !draft.HasPending });
    protected override bool OnBackButtonPressed()
    {
        if (!draft.HasPending) return base.OnBackButtonPressed();
        Status.Text = "Yêu cầu chưa xác nhận. Retry cùng form; không đóng để mất key. Có thể đăng xuất để bỏ bản RAM, operation backend vẫn giữ.";
        return true;
    }
    private static void Rebind(Picker picker, IReadOnlyList<AssetChoice> choices, string? selected)
    { picker.ItemsSource = choices.ToArray(); picker.SelectedItem = choices.FirstOrDefault(x => x.Id == selected); }
    private void Pick(string title, Picker picker, IReadOnlyList<AssetChoice> choices, string selected)
    { Body.Add(new Label { Text = title }); picker.ItemsSource = choices.ToArray(); picker.SelectedItem = choices.FirstOrDefault(x => x.Id == selected); Body.Add(picker); }
    private AssetInput ReadInput()
    {
        DateOnly? recorded = null;
        if (!string.IsNullOrWhiteSpace(date.Text))
        { if (!DateOnly.TryParseExact(date.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) throw new MobileApiException(422, "Ngày ghi nhận phải yyyy-MM-dd hoặc để trống."); recorded = parsed; }
        return new(name.Text ?? "", (category.SelectedItem as AssetChoice)?.Id ?? "", (org.SelectedItem as AssetChoice)?.Id ?? "",
            (location.SelectedItem as AssetChoice)?.Id ?? "", "MACHINE", custodian.Text ?? "", serial.Text ?? "", model.Text ?? "", manufacturer.Text ?? "",
            recorded, condition.Text ?? "", notes.Text ?? "", (aliases.Text ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }
}
