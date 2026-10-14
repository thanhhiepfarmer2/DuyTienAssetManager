using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.Infrastructure.Local;
using DuyTien.AssetManagement.Server.Categories;
using Microsoft.AspNetCore.DataProtection;
namespace DuyTien.AssetManagement.Server.LocalOffline;

public static class LocalOfflineRegistration
{
    public static void AddLocalOffline(this WebApplicationBuilder builder)
    {
        var root = Path.GetFullPath(builder.Configuration["Storage:WorkspacePath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DuyTienAssets", "LocalOffline", "default"));
        var repository = Path.GetFullPath(builder.Environment.ContentRootPath);
        while (!File.Exists(Path.Combine(repository, "AGENTS.md")) && Directory.GetParent(repository) is { } parent) repository = parent.FullName;
        if (root.StartsWith(repository + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || root == repository || root.StartsWith(@"\\", StringComparison.Ordinal)) throw new InvalidOperationException("LocalOffline workspace phải ở local disk ngoài repository.");
        builder.Services.PostConfigure<Authentication.AuthRuntimeOptions>(o => o.SystemWriterLockFilePath = Path.Combine(root, "auth-writer.lock"));
        builder.Services.AddSingleton(_ => new LocalWorkspace(root));
        builder.Services.AddSingleton<IAssetCatalogStore>(s => s.GetRequiredService<LocalWorkspace>());
        builder.Services.AddSingleton<ICategoryCatalogStore>(s => s.GetRequiredService<LocalWorkspace>());
        builder.Services.AddSingleton<IInventoryStore>(s => s.GetRequiredService<LocalWorkspace>());
        builder.Services.AddSingleton<IMediaCatalogStore>(s => s.GetRequiredService<LocalWorkspace>());
        builder.Services.AddSingleton<ITreeMutationStore>(s => s.GetRequiredService<LocalWorkspace>());
        builder.Services.AddSingleton<IManagedMediaContentStore>(s => s.GetRequiredService<LocalWorkspace>());
        builder.Services.AddSingleton<IAuthSystemStore>(s => new RequestAuthStore(s.GetRequiredService<LocalWorkspace>()));
        builder.Services.AddSingleton<ISingleWriterCoordinator>(s => s.GetRequiredService<LocalWorkspace>());
        builder.Services.AddSingleton<LocalDemoInitializer>();
        builder.Services.AddSingleton<AssetCrudService>(); builder.Services.AddSingleton<CategoryCatalogService>(); builder.Services.AddSingleton<TreeMutationService>();
        builder.Services.AddSingleton<InventoryService>(); builder.Services.AddSingleton<ReportService>(); builder.Services.AddSingleton<MediaLibraryService>(); builder.Services.AddSingleton<CategoryPreviewCache>();
        builder.Services.Configure<MediaUploadOptions>(builder.Configuration.GetSection(MediaUploadOptions.SectionName)); builder.Services.AddSingleton(s => s.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaUploadOptions>>().Value);
        builder.Services.AddHttpClient(); builder.Services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => new OfflineOutboundGuard()));
        var protection = builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(root, "keys"))).SetApplicationName("DuyTien.LocalOffline." + root);
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
    }
}
public sealed class OfflineOutboundGuard : HttpMessageHandler
{
    public static long Attempts => Interlocked.Read(ref attempts);
    private static long attempts;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { Interlocked.Increment(ref attempts); throw new HttpRequestException("LocalOffline không cho outbound HTTP. Chọn Google mode chủ động cho import/export tương lai."); }
}
