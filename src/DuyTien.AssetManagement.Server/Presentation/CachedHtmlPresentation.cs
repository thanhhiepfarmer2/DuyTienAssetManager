using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Server.ConnectedCache;
namespace DuyTien.AssetManagement.Server.Presentation;

// Presentation compatibility only. Protected ticket/session/scope/expiry are checked by ConnectedRuntime first.
// Old bytes stay intact; no old scripts, forms or fingerprint URLs execute. JSON and file bytes are unchanged.
public static class CachedHtmlPresentation
{
    public static bool IsHtml(string type) => type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
    public static bool IsCurrent(CachedView view) => !IsHtml(view.ContentType) || view.PresentationVersion == WebUi.CacheRevision;
    private static string Replace(string text, string pattern, string value) => Regex.Replace(text, pattern, value, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(2));
    public static string Text(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        foreach (var tag in new[] { "head", "script", "style" }) text = Replace(text, "<" + tag + @"\b[^>]*>.*?</" + tag + @"\s*>", "");
        text = Replace(text, @"<!--.*?-->", "");
        text = Replace(text, @"</(td|th)\s*>", " | ");
        text = Replace(text, @"<(br|/p|/div|/h[1-6]|/tr|/li|/section)\b[^>]*>", "\n");
        return WebUtility.HtmlDecode(Replace(text, "<[^>]+>", ""));
    }
    public static string Legacy(byte[] bytes, DateTimeOffset asOf, IEnumerable<string>? files = null) =>
        "<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Bản lưu giao diện cũ · Duy Tiến Assets</title><link rel=\"stylesheet\" href=\"/css/site.css\"></head><body class=\"offline-page\" data-connection-state=\"OFFLINE_READ_ONLY\"><h1>Ngoại tuyến — chỉ đọc</h1><div role=\"status\">Bản lưu thuộc phiên bản giao diện cũ. Nội dung được giữ ở dạng chỉ đọc; kết nối online để cập nhật trang này. Không phải dữ liệu hiện hành.</div><p>Thời điểm bản lưu: " + WebUtility.HtmlEncode(asOf.ToString("O")) + "</p><p><a href=\"/Offline\">Về dữ liệu đã lưu và đăng xuất</a></p><pre class=\"legacy-cache-text\">" + WebUtility.HtmlEncode(Text(bytes)) + "</pre>" + FileLinks(files) + "<script src=\"/js/connection.js\"></script></body></html>";
    private static string FileLinks(IEnumerable<string>? files)
    {
        var links = (files ?? []).Where(url => url.StartsWith("/Files/Content/", StringComparison.OrdinalIgnoreCase) || url.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase)).Distinct().Take(200).Select((url, i) => "<li><a href=\"" + WebUtility.HtmlEncode(url) + "\">Tệp đã lưu " + (i + 1) + "</a></li>").ToArray();
        return links.Length == 0 ? "<p>Chưa có tệp đã lưu cho phiên này.</p>" : "<h2>Tệp đã có trên máy trong phiên này</h2><ul>" + string.Join("", links) + "</ul>";
    }

}
