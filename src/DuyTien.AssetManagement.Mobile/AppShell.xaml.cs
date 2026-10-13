namespace DuyTien.AssetManagement.Mobile;

public partial class AppShell : Shell
{
    public AppShell(MainPage main) { InitializeComponent(); Items.Add(new ShellContent { Title = "Duy Tiến", Content = main }); }
}
