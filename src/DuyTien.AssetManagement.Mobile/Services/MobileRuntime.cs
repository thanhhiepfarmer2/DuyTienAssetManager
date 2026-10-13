using DuyTien.AssetManagement.Mobile.Client;
namespace DuyTien.AssetManagement.Mobile.Services;

public sealed class MobileRuntime
{
    private HttpClient? http;
    public MobileSession? Session { get; private set; }
    public MobileAssets Assets => new(Session ?? throw new MobileApiException(401, "Cần đăng nhập."));
    public string BackendUrl => Preferences.Default.Get("duytien.backend.url", "");
    public event Action? SessionEnded;
    public void Configure(string value)
    {
        if (Session?.IsSignedIn == true) throw new MobileApiException(409, "Đăng xuất trước khi đổi backend.");
        var url = MobileEndpoint.Parse(value);
        http?.Dispose();
        http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(45) };
        Session = new(http, url, new MauiSecretStore(), TimeProvider.System);
        Session.SessionEnded += () => SessionEnded?.Invoke();
        Preferences.Default.Set("duytien.backend.url", url.AbsoluteUri);
    }
}
