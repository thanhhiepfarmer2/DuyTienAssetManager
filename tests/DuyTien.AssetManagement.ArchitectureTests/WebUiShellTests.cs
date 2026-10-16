using System.Net;
using System.Security.Claims;
using DuyTien.AssetManagement.Server.Presentation;
namespace DuyTien.AssetManagement.ArchitectureTests;

// Compiled Razor + synthetic transport only. No browser/Google evidence.
public sealed class WebUiShellTests
{
    [Fact]
    public async Task Anonymous_shell_has_local_brand_assets_keyboard_menu_and_no_business_links()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = h.Client();
        var html = WebUtility.HtmlDecode(await c.GetStringAsync("/Account/Login"));
        Assert.Contains("Duy Tiến Assets", html); Assert.Contains("main-navigation", html); Assert.Contains("aria-expanded=\"false\"", html);
        Assert.Contains("Đến nội dung chính", html); Assert.Contains("auth-panel", html); Assert.Contains("/js/ui.js", html); Assert.Contains("/img/icons.svg", html);
        Assert.DoesNotContain("href=\"/Assets\"", html); Assert.DoesNotContain("href=\"/Admin/Users\"", html);
        Assert.DoesNotContain("cdn.", html); Assert.DoesNotContain("LocalSetup", html); Assert.DoesNotContain("Demo DRAFT", html);
    }
    [Theory]
    [InlineData("/Categories")][InlineData("/Assets")][InlineData("/Documents")][InlineData("/Items")][InlineData("/Stock")][InlineData("/Reports")][InlineData("/Admin/Users")]
    public async Task Authenticated_pages_share_shell_and_active_route_without_private_transport_details(string path)
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin");
        var html = WebUtility.HtmlDecode(await c.GetStringAsync(path));
        Assert.Contains("aria-current=\"page\"", html); Assert.Contains("sidebar-link active", html); Assert.Contains("breadcrumb", html);
        Assert.Contains("data-viewer=", html); Assert.Contains("data-session=", html); Assert.Contains("Nguồn dữ liệu chưa xác nhận", html);
        Assert.DoesNotContain("PRIVATE-", html); Assert.DoesNotContain("fixture-token", html);
    }
    [Fact]
    public void Menu_and_labels_are_pure_presentation_do_not_add_authority_or_mutate_state()
    {
        var viewer = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, "Viewer")], "FAKE"));
        Assert.DoesNotContain(WebUi.Navigation(viewer), n => n.Url.StartsWith("/Admin"));
        Assert.Contains(WebUi.Navigation(viewer), n => n.Url == "/Assets");
        Assert.False(WebUi.Active("/AssetsOther", "/Assets")); Assert.True(WebUi.Active("/Assets/Detail/FAKE", "/Assets"));
        Assert.Equal("Chưa có số lượng", WebUi.State("UNKNOWN")); Assert.Equal("Dữ liệu kiểm thử", WebUi.State("DRAFT_DEMO"));
        Assert.Equal("Số lượng", WebUi.Labels["Quantity"]); Assert.Equal("Chưa có ngưỡng", WebUi.State("MISSING_MINIMUM"));
    }
}

public sealed class WebUiModuleTests
{
    [Fact]
    public async Task Admin_forms_have_real_confirmations_and_help_matches_read_only_connection_policy()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin");
        var admin = WebUtility.HtmlDecode(await c.GetStringAsync("/Admin/Users/Detail/ADMIN"));
        foreach (var handler in new[] { "Lock", "Reset", "Assign" }) Assert.Contains("handler=" + handler, admin);
        Assert.Contains("data-confirm=", admin); Assert.Contains("Phân quyền đã lưu", admin); Assert.Contains("Hiệu lực từ", admin); Assert.Contains("ALL", admin);
        var help = WebUtility.HtmlDecode(await c.GetStringAsync("/Help"));
        Assert.Contains("Ngoại tuyến — chỉ đọc", help); Assert.Contains("tối thiểu 8 ký tự", help); Assert.DoesNotContain("LocalSetup", help); Assert.DoesNotContain("Internet không cần cho login/lưu", help);
        var home = WebUtility.HtmlDecode(await c.GetStringAsync("/")); Assert.Contains("work-grid", home); Assert.DoesNotContain("Tạo máy demo", home); Assert.DoesNotContain("Android", home);
        var error = WebUtility.HtmlDecode(await c.GetStringAsync("/Error")); Assert.Contains("Chưa thể hoàn tất", error); Assert.DoesNotContain("Development Mode", error);
    }
    [Fact]
    public async Task Category_and_asset_views_expose_real_controls_preserve_provenance_and_form_contract()
    {
        await using var h = await WebIntegrationHost.Start(); using var c = await h.Cookie("admin");
        var tree = WebUtility.HtmlDecode(await c.GetStringAsync("/Categories"));
        Assert.Contains("<td>F1</td>", tree); Assert.DoesNotContain("F@n.", tree);
        var category = WebUtility.HtmlDecode(await c.GetStringAsync("/Categories/Detail/" + h.R.F.Root));
        Assert.Contains("<dd>F1 /", category); Assert.DoesNotContain("F@n.", category);
        var breadcrumb = System.Text.RegularExpressions.Regex.Match(tree, "<nav\\b[^>]*class=\"breadcrumb\"[^>]*>.*?</nav>", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        Assert.NotEmpty(breadcrumb); Assert.DoesNotContain("href=\"/Categories\"", breadcrumb); Assert.Matches("<span[^>]*aria-current=\"page\"[^>]*>Danh mục</span>", breadcrumb);
        Assert.Contains("data-category-tree", tree); Assert.Contains("data-depth=", tree); Assert.Contains("Chưa duyệt", tree); Assert.DoesNotContain("Cây nháp phục vụ demo", tree);
        var detail = WebUtility.HtmlDecode(await c.GetStringAsync("/Assets/Detail/" + h.R.Asset.AssetId));
        Assert.Contains("data-confirm=", detail); Assert.Contains("name=\"ExpectedVersion\"", detail); Assert.Contains("name=\"RequestKey\"", detail); Assert.Contains(h.R.Asset.ObjectCode, detail);
        Assert.Contains("Dữ liệu kiểm thử", detail); Assert.DoesNotContain("Sửa máy demo", detail);
        var create = WebUtility.HtmlDecode(await c.GetStringAsync("/Assets/Create")); Assert.Contains("Thêm tài sản", create); Assert.Contains("name=\"TreeVersionId\"", create);
    }
}
