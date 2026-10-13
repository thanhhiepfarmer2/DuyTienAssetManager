using DuyTien.AssetManagement.Mobile.Client;
using DuyTien.AssetManagement.Mobile.Pages;
using DuyTien.AssetManagement.Mobile.Services;
namespace DuyTien.AssetManagement.Mobile;

public partial class MainPage : ContentPage
{
    private readonly MobileRuntime runtime;
    private readonly Label status = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly ActivityIndicator activity = new();
    private bool loaded;
    private bool busy;
    private bool endingSession;
    public MainPage(MobileRuntime runtime)
    {
        InitializeComponent(); this.runtime = runtime;
        runtime.SessionEnded += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var page in Navigation.NavigationStack.OfType<MobilePage>()) page.EndSession();
            _ = EndSessionAsync();
        });
        ShowLogin();
    }
    private async Task EndSessionAsync()
    {
        if (endingSession) return; endingSession = true;
        try { if (Navigation.NavigationStack.Count > 1) await Navigation.PopToRootAsync(false); }
        catch (InvalidOperationException) { /* Screens already cleared; navigation may be transitioning. */ }
        finally { ShowLogin(); status.Text = "Phiên đã kết thúc. Đăng nhập lại."; endingSession = false; }
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); if (loaded) return; loaded = true;
        if (string.IsNullOrWhiteSpace(runtime.BackendUrl)) return;
        await Run(async () =>
        {
            runtime.Configure(runtime.BackendUrl);
            if (await runtime.Session!.RestoreAsync()) await ShowSession();
            else status.Text = "Nhập tài khoản ứng dụng để đăng nhập.";
        });
    }
    private void Reset(string title)
    {
        Root.Clear(); Root.Add(new Label { Text = title, FontSize = 23 });
        Root.Add(new Label { Text = "DRAFT_DEMO — dữ liệu và quy tắc công ty chưa được phê duyệt.", TextColor = Colors.DarkOrange });
    }
    private Entry Field(string label, string value = "", bool password = false)
    {
        Root.Add(new Label { Text = label }); var e = new Entry { Text = value, IsPassword = password }; SemanticProperties.SetDescription(e, label); Root.Add(e); return e;
    }
    private void Button(string label, Func<Task> callback)
    {
        var b = new Button { Text = label, MinimumHeightRequest = 48 };
        b.Clicked += async (_, _) => await Run(callback); Root.Add(b);
    }
    private void Footer() { Root.Add(activity); Root.Add(status); }
    private void ShowLogin()
    {
        Reset("Đăng nhập mobile");
        var url = Field("Backend URL theo môi trường (HTTPS)", runtime.BackendUrl);
        var username = Field("Tên đăng nhập"); var password = Field("Mật khẩu", password: true);
        Button("Đăng nhập", async () =>
        {
            runtime.Configure(url.Text ?? "");
            try { await runtime.Session!.LoginAsync(username.Text ?? "", password.Text ?? ""); await ShowSession(); }
            finally { password.Text = ""; }
        });
        Button("Thử lại phiên đã đăng nhập", async () => { if (runtime.Session?.IsSignedIn != true) throw new MobileApiException(401, "Chưa có phiên; đăng nhập trước."); await ShowSession(); });
        Button("Xóa phiên / đăng xuất", async () => { if (runtime.Session is not null) await Logout(); });
        Root.Add(new Label { Text = "App chỉ gọi backend. Host/TLS phải được thiết bị tin cậy; không dùng chứng chỉ bỏ kiểm tra. Không lưu mật khẩu." }); Footer();
    }
    private async Task ShowSession()
    {
        var me = await runtime.Assets.MeAsync();
        if (me.MustChangePassword || runtime.Session!.MustChangePassword) { ShowPassword(); return; }
        Reset("Đã đăng nhập: " + me.DisplayName);
        Root.Add(new Label { Text = "Role: " + string.Join(", ", me.Roles) + ". Mọi hành động và phạm vi kiểm phía server." });
        Button("Danh sách tài sản", async () => await Navigation.PushAsync(new AssetListPage(runtime, me)));
        Button("Đổi mật khẩu", () => { ShowPassword(); return Task.CompletedTask; });
        Button("Đăng xuất", Logout); Footer(); status.Text = "Phiên mobile sẵn sàng.";
    }
    private void ShowPassword()
    {
        Reset("Đổi mật khẩu");
        Root.Add(new Label { Text = "Mật khẩu mới tối thiểu8 ký tự. Sau đổi phải đăng nhập lại." });
        var current = Field("Mật khẩu hiện tại", password: true); var next = Field("Mật khẩu mới", password: true); var confirm = Field("Nhập lại mật khẩu mới", password: true);
        Button("Đổi mật khẩu", async () =>
        {
            if (next.Text != confirm.Text) throw new MobileApiException(400, "Hai mật khẩu mới chưa khớp.");
            try { await runtime.Session!.ChangePasswordAsync(current.Text ?? "", next.Text ?? ""); ShowLogin(); status.Text = "Đổi mật khẩu thành công. Đăng nhập lại."; }
            finally { current.Text = next.Text = confirm.Text = ""; }
        });
        Button("Đăng xuất", Logout); Footer(); status.Text = "Đổi mật khẩu để tiếp tục.";
    }
    private async Task Logout()
    {
        try { await runtime.Session!.LogoutAsync(); ShowLogin(); status.Text = "Đã đăng xuất."; }
        catch (MobileApiException) { ShowLogin(); status.Text = "Phiên RAM đã ngừng. Chưa xác nhận được logout/xóa vùng bảo mật; backend vẫn theo chính sách phiên hiện hành."; }
    }
    private async Task Run(Func<Task> action)
    {
        if (busy) return; busy = true; Root.IsEnabled = false; activity.IsRunning = true; status.Text = "Đang xử lý…";
        try { await action(); }
        catch (MobileApiException e) { status.Text = e.Message; }
        catch { status.Text = "Không hoàn tất thao tác; chưa xác nhận thành công. Thử lại."; }
        finally { Root.IsEnabled = true; activity.IsRunning = false; busy = false; }
    }
}
