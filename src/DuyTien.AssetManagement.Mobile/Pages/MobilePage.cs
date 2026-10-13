using DuyTien.AssetManagement.Mobile.Client;
using DuyTien.AssetManagement.Mobile.Services;
namespace DuyTien.AssetManagement.Mobile.Pages;

public abstract class MobilePage : ContentPage
{
    protected readonly MobileRuntime Runtime;
    protected readonly VerticalStackLayout Body = new() { Padding = 20, Spacing = 12 };
    protected readonly Label Status = new() { LineBreakMode = LineBreakMode.WordWrap, Margin = 12 };
    private readonly ActivityIndicator activity = new();
    private bool busy;
    protected MobilePage(MobileRuntime runtime, string title)
    {
        Runtime = runtime; Title = title;
        var footer = new VerticalStackLayout { Children = { activity, Status } };
        var grid = new Grid { RowDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(new ScrollView { Content = Body }, 0, 0); grid.Add(footer, 0, 1); Content = grid;
    }
    public void EndSession() { Body.Clear(); Status.Text = "Phiên đã kết thúc. Dữ liệu trên màn hình đã xóa."; Body.IsEnabled = false; }
    protected async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; Body.IsEnabled = false; activity.IsRunning = true; Status.Text = "Đang xử lý…";
        try { await action(); }
        catch (MobileApiException e) { Status.Text = e.Message; }
        catch { Status.Text = "Không hoàn tất thao tác. Giữ nội dung và thử lại; chưa báo lưu thành công."; }
        finally { busy = false; Body.IsEnabled = true; activity.IsRunning = false; }
    }
    protected Button Action(string text, Func<Task> action)
    {
        var button = new Button { Text = text, MinimumHeightRequest = 48 };
        button.Clicked += async (_, _) => await RunAsync(action); Body.Add(button); return button;
    }
    protected Entry Field(string label, string value = "", bool password = false)
    {
        Body.Add(new Label { Text = label }); var entry = new Entry { Text = value, IsPassword = password, ClearButtonVisibility = password ? ClearButtonVisibility.Never : ClearButtonVisibility.WhileEditing };
        SemanticProperties.SetDescription(entry, label); Body.Add(entry); return entry;
    }
}
