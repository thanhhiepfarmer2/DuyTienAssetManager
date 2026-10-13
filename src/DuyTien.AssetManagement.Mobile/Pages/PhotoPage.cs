using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Mobile.Client;
using DuyTien.AssetManagement.Mobile.Services;
namespace DuyTien.AssetManagement.Mobile.Pages;

public sealed class PhotoPage : MobilePage
{
    private readonly MobilePhotoDraft draft;
    private readonly IMobilePhotoPicker picker = new MauiPhotoPicker();
    private readonly Image preview = new() { HeightRequest = 240, Aspect = Aspect.AspectFit };
    private readonly VerticalStackLayout photos = new() { Spacing = 8 };
    private readonly Entry description;
    private readonly Label uploadState = new();
    private readonly bool canUpload;
    private readonly CheckBox primary = new();
    private readonly string assetId;
    private bool loaded;
    public PhotoPage(MobileRuntime runtime, AssetRecord asset, MobileIdentity me) : base(runtime, "Ảnh — " + asset.ObjectCode)
    {
        canUpload = me.Roles.Any(x => x is "Admin" or "AssetEditor");
        assetId = asset.AssetId; draft = new(runtime.Assets, assetId, asset.Version);
        Body.Add(new Label { Text = "Ảnh PNG/JPEG ≤10MiB. DRAFT_DEMO. Chỉ AVAILABLE từ backend mới là upload thành công; chọn/hủy không tự upload." });
        Body.Add(preview); Body.Add(uploadState);
        description = Field("Mô tả ảnh"); Body.Add(new HorizontalStackLayout { Children = { primary, new Label { Text = "Ảnh chính", VerticalOptions = LayoutOptions.Center } } });
        if (canUpload)
        {
            Action("Chụp ảnh", () => Pick(PhotoSource.Camera)); Action("Chọn ảnh", () => Pick(PhotoSource.Library));
            Action("Upload / retry cùng ảnh và key", async () =>
            {
                draft.Description = description.Text ?? ""; draft.IsPrimary = primary.IsChecked;
                try { var result = await draft.UploadAsync(); Status.Text = "AVAILABLE — đã lưu ảnh (" + result.File.FileId + ")."; }
                finally { description.IsEnabled = primary.IsEnabled = !draft.HasPending; UpdateState(); }
            });
            Action("Đối soát upload chưa xác nhận", async () =>
            { var result = await draft.ReconcileAsync(); description.IsEnabled = primary.IsEnabled = true; UpdateState(); Status.Text = "AVAILABLE — backend đã xác nhận ảnh " + result.File.FileId; });
        }
        if (canUpload)
        {
            Action("Sau409 chưa có file: nạp owner version mới", async () => { await draft.RebaseAsync(); description.IsEnabled = primary.IsEnabled = true; UpdateState(); Status.Text = "Đã nạp version mới, giữ ảnh/mô tả. Kiểm tra rồi upload."; });
            Action("Đăng xuất / bỏ ảnh RAM", async () => await Runtime.Session!.LogoutAsync());
        }
        Body.Add(new Label { Text = "Ảnh trong phạm vi hiện tại" }); Body.Add(photos);
        Action("Tải lại danh sách ảnh", Load);
        Body.Add(new Label { Text = "Form/ảnh chờ chỉ giữ trong RAM. Không đóng trang đang chờ để mất key; đóng app/đăng xuất mất bản RAM, không tự tạo upload khác. Backend giữ operation PENDING/FAILED để đối soát.409: chưa upload lại, tải tài sản/version hiện tại trước thao tác mới." });
    }
    private void UpdateState()
    {
        uploadState.Text = "Upload: " + draft.State + (draft.FileId is null ? "" : " / " + draft.FileId);
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsEnabled = !draft.HasPending, IsVisible = !draft.HasPending });
    }
    protected override bool OnBackButtonPressed()
    {
        if (!draft.HasPending) return base.OnBackButtonPressed();
        Status.Text = "Upload chưa xác nhận. Retry/đối soát để giữ operation, hoặc đăng xuất bỏ bản RAM."; return true;
    }
    private async Task Pick(PhotoSource source)
    {
        if (await draft.PickAsync(picker, source)) { Show(draft.Photo!.Bytes); UpdateState(); Status.Text = "Đã chọn ảnh, chưa upload."; }
        else Status.Text = "Đã hủy chọn/chụp; ảnh và nội dung trước vẫn giữ.";
    }
    private void Show(byte[] bytes) => preview.Source = ImageSource.FromStream(() => new MemoryStream(bytes, false));
    protected override async void OnAppearing() { base.OnAppearing(); if (!loaded) { loaded = true; await RunAsync(Load); } }
    private async Task Load()
    {
        photos.Clear(); preview.Source = draft.Photo is null ? null : ImageSource.FromStream(() => new MemoryStream(draft.Photo.Bytes, false));
        foreach (var file in await Runtime.Assets.PhotosAsync(assetId))
        {
            var button = new Button { Text = file.FileName + " / " + file.State + (file.IsPrimary ? " / ảnh chính" : ""), MinimumHeightRequest = 48 };
            button.Clicked += async (_, _) => await RunAsync(async () =>
            {
                if (file.State != "AVAILABLE") throw new MobileApiException(409, "Ảnh chưa AVAILABLE; cần retry/đối soát.");
                preview.Source = null; Show(await Runtime.Session!.PhotoAsync(file.FileId)); Status.Text = "Đã tải preview qua gateway theo quyền hiện tại.";
            }); photos.Add(button);
            if (canUpload && file.State is "PENDING" or "FAILED")
            {
                var reconcile = new Button { Text = "Đối soát " + file.FileName, MinimumHeightRequest = 48 };
                reconcile.Clicked += async (_, _) => await RunAsync(async () => { await Runtime.Assets.ReconcileAsync(file.FileId); await Load(); });
                photos.Add(reconcile);
            }
        }
        Status.Text = photos.Children.Count == 0 ? "Chưa có ảnh trong phạm vi." : "Đã tải metadata ảnh; chọn để xem.";
    }
}
