namespace DuyTien.AssetManagement.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly MainPage main;
    public App(MainPage main) { InitializeComponent(); this.main = main; }
    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell(main));
}
