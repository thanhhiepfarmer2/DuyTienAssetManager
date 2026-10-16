using DuyTien.AssetManagement.Server.ConnectedCache;
using DuyTien.AssetManagement.Server.LocalOffline;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.Server.Reports;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Server.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Server.Media;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Server.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Server.Categories;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Application.ProofOfConcept;
using DuyTien.AssetManagement.Infrastructure.Google;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Concurrency;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
using DuyTien.AssetManagement.Server;
using DuyTien.AssetManagement.Server.Authentication;
using DuyTien.AssetManagement.Server.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var storageMode = builder.Configuration["Storage:Mode"] ?? "Google";
if (storageMode is not ("Google" or "LocalOffline")) throw new InvalidOperationException("Storage:Mode phải là Google hoặc LocalOffline, không fallback.");
var localOffline = storageMode == "LocalOffline";
if (localOffline) throw new InvalidOperationException("LocalOffline ghi độc lập đã ngừng theo REQ-002 v2. Dùng DuyTien-Web.cmd; workspace cũ được bảo toàn, không LocalSetup.");
var connectedCache = builder.Configuration.GetValue<bool?>("ConnectedCache:Enabled") ?? true;
if (!connectedCache) throw new InvalidOperationException("Connection/read-only guard bắt buộc theo REQ-002 v2.");
if (connectedCache) builder.AddConnectedCache();
if (localOffline)
{
    foreach (var source in builder.Configuration.Sources.OfType<Microsoft.Extensions.Configuration.Json.JsonConfigurationSource>().Where(s => Path.GetFileName(s.Path) == "secrets.json").ToArray()) builder.Configuration.Sources.Remove(source);
    if (args.FirstOrDefault()?.StartsWith("google-", StringComparison.Ordinal) == true) throw new InvalidOperationException("Google CLI không chạy trong LocalOffline mode.");
}
var configuredMinimumPasswordLength = builder.Configuration.GetValue<int?>(
    "Auth:Password:MinimumLength") ?? AuthPasswordPolicy.MinimumLength;
if (configuredMinimumPasswordLength != AuthPasswordPolicy.MinimumLength)
{
    throw new InvalidOperationException(
        "Auth:Password:MinimumLength must remain 8 for the accepted TASK-005 policy.");
}

builder.Services.AddRazorPages().AddMvcOptions(o => { o.Filters.Add<PerformanceResultFilter>(); o.ModelMetadataDetailsProviders.Add(new DuyTien.AssetManagement.Server.Presentation.WebUiMetadataProvider()); });
builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(
        OpaqueSessionAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
if (!localOffline)
{
    builder.Services.Configure<GoogleWorkspaceOptions>(
        builder.Configuration.GetSection(GoogleWorkspaceOptions.SectionName));
    builder.Services.AddSingleton(
        serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<GoogleWorkspaceOptions>>().Value);
    builder.Services.AddSafeGoogleHttpClient("GoogleOAuth", TimeSpan.FromSeconds(30));
    builder.Services.AddSafeGoogleHttpClient("GoogleApi", TimeSpan.FromSeconds(30));
    builder.Services.AddSingleton<IGoogleAccessTokenProvider>(serviceProvider =>
        new GoogleOAuthTokenProvider(
            serviceProvider.GetRequiredService<GoogleWorkspaceOptions>(),
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleOAuth")));
    builder.Services.AddSingleton(serviceProvider => new GoogleSheetsRequestBudget(
        serviceProvider.GetRequiredService<IOptions<AuthRuntimeOptions>>().Value.SystemWriterLockFilePath + ".sheets-budget.json"));
    builder.Services.AddSingleton(serviceProvider =>
        new GoogleApiRequestExecutor(
            serviceProvider.GetRequiredService<GoogleWorkspaceOptions>(),
            serviceProvider.GetRequiredService<IGoogleAccessTokenProvider>(),
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleApi"),
            sheetsBudget: serviceProvider.GetRequiredService<GoogleSheetsRequestBudget>(),
            connectivity: serviceProvider.GetService<IGoogleConnectivityGuard>()));
    builder.Services.AddSingleton<GoogleSheetsDataStore>();
    builder.Services.AddSingleton<GoogleAuthSystemStore>();
    builder.Services.AddSingleton<GoogleAssetCatalogStore>();
    builder.Services.AddSingleton<IAssetCatalogStore>(p => p.GetRequiredService<ObservedBusinessStores>());
    builder.Services.AddSingleton<AssetCrudService>();
    builder.Services.AddSingleton<GoogleInventoryStore>();
    builder.Services.AddSingleton<IInventoryStore>(p => p.GetRequiredService<ObservedBusinessStores>());
    builder.Services.AddSingleton<InventoryService>();
    builder.Services.AddSingleton<ReportService>();
    builder.Services.AddSingleton<GoogleCategoryCatalogStore>();
    builder.Services.AddSingleton<ICategoryCatalogStore>(p => p.GetRequiredService<ObservedBusinessStores>());
    builder.Services.AddSingleton<GoogleTreeMutationStore>();
    builder.Services.AddSingleton<ITreeMutationStore>(p => p.GetRequiredService<ObservedBusinessStores>());
    builder.Services.AddSingleton<GoogleWebDevInitializer>();
    builder.Services.AddSingleton<TreeMutationService>();
    builder.Services.Configure<MediaUploadOptions>(builder.Configuration.GetSection(MediaUploadOptions.SectionName));
    builder.Services.AddSingleton(p => p.GetRequiredService<IOptions<MediaUploadOptions>>().Value);
    builder.Services.AddSingleton<GoogleMediaCatalogStore>();
    builder.Services.AddSingleton<IMediaCatalogStore>(p => p.GetRequiredService<ObservedBusinessStores>());
    builder.Services.AddSingleton<IManagedMediaContentStore, GoogleManagedMediaContentStore>();
    builder.Services.AddSingleton<MediaLibraryService>();
    builder.Services.AddSingleton<CategoryCatalogService>();
    builder.Services.AddSingleton<CategoryPreviewCache>();
    builder.Services.AddSingleton<IAuthSystemStore>(serviceProvider =>
        new RequestAuthStore(serviceProvider.GetRequiredService<GoogleAuthSystemStore>()));
    builder.Services.AddSingleton<IAssetDataStore>(serviceProvider =>
        serviceProvider.GetRequiredService<GoogleSheetsDataStore>());
    builder.Services.AddSingleton<IPocAtomicDataStore>(serviceProvider =>
        serviceProvider.GetRequiredService<GoogleSheetsDataStore>());
    builder.Services.AddSingleton<IPocFileStateStore>(serviceProvider =>
        serviceProvider.GetRequiredService<GoogleSheetsDataStore>());
    builder.Services.AddSingleton<GoogleDriveFileStore>();
    builder.Services.AddSingleton<IAssetFileStore>(serviceProvider =>
        serviceProvider.GetRequiredService<GoogleDriveFileStore>());
    builder.Services.AddSingleton<GoogleDriveAccessVerifier>();
    builder.Services.AddSingleton<GoogleWorkspaceProvisioner>();
    builder.Services.AddSingleton<ObservedBusinessStores>(p => new(p.GetRequiredService<GoogleAssetCatalogStore>(), p.GetRequiredService<GoogleCategoryCatalogStore>(), p.GetRequiredService<GoogleInventoryStore>(), p.GetRequiredService<GoogleMediaCatalogStore>(), p.GetRequiredService<GoogleTreeMutationStore>()));
    builder.Services.AddSingleton<ISingleWriterCoordinator, InProcessSingleWriterCoordinator>();
}
else builder.AddLocalOffline();
builder.Services.Configure<AuthRuntimeOptions>(
    builder.Configuration.GetSection(AuthRuntimeOptions.SectionName));
builder.Services.AddSingleton(serviceProvider =>
    serviceProvider.GetRequiredService<IOptions<AuthRuntimeOptions>>().Value);
builder.Services.AddSingleton<IAuthWriterLeaseManager, FileAuthWriterLeaseManager>();
builder.Services.AddSingleton<IPasswordHashService, FrameworkPasswordHashService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new AuthPolicyOptions
{
    MaxFailedAttempts = builder.Configuration.GetValue<int?>("Auth:Lockout:MaxFailedAttempts") ?? 5,
    LockoutDuration = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int?>("Auth:Lockout:DurationMinutes") ?? 15),
    LoginRateLimit = builder.Configuration.GetValue<int?>("Auth:RateLimit:LoginAttempts") ?? 10,
    LoginRateWindow = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int?>("Auth:RateLimit:WindowMinutes") ?? 15),
    WebIdle = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int?>("Auth:Sessions:WebIdleMinutes") ?? 30),
    WebAbsolute = TimeSpan.FromHours(
        builder.Configuration.GetValue<int?>("Auth:Sessions:WebAbsoluteHours") ?? 8),
    RememberIdle = TimeSpan.FromHours(
        builder.Configuration.GetValue<int?>("Auth:Sessions:RememberIdleHours") ?? 24),
    RememberAbsolute = TimeSpan.FromDays(
        builder.Configuration.GetValue<int?>("Auth:Sessions:RememberAbsoluteDays") ?? 7),
    MobileAccess = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int?>("Auth:Sessions:MobileAccessMinutes") ?? 15),
    MobileRefreshIdle = TimeSpan.FromDays(
        builder.Configuration.GetValue<int?>("Auth:Sessions:MobileRefreshIdleDays") ?? 14),
    MobileRefreshAbsolute = TimeSpan.FromDays(
        builder.Configuration.GetValue<int?>("Auth:Sessions:MobileRefreshAbsoluteDays") ?? 30),
    MaxActiveSessionsPerUser = builder.Configuration.GetValue<int?>("Auth:Sessions:MaxActivePerUser") ?? 5
});
builder.Services.AddSingleton<AuthApplicationService>();
if (!localOffline)
{
    builder.Services.AddSingleton<PocAssetWriteService>();
    builder.Services.AddSingleton<PocFileWriteService>();
}
if (!localOffline) builder.Services.AddSingleton(serviceProvider =>
    new GoogleOAuthBootstrapper(
        serviceProvider.GetRequiredService<GoogleWorkspaceOptions>(),
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleOAuth")));

var app = builder.Build();
using var commandOperation = ConnectionGate.BeginOperation();

if (!localOffline && await GoogleWorkspaceCommandRunner.TryRunAsync(args, app.Services))
{
    return;
}

if (!localOffline && await WebDevCommandRunner.TryRunAsync(args, app.Services, app.Environment))
{
    return;
}

if (await AuthCommandRunner.TryRunAsync(args, app.Services))
{
    return;
}

app.UseExceptionHandler("/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UsePerformanceTrace();
app.UseStaticFiles();
app.UseRouting();
app.Use(async (context, next) => { using var scope = RequestAuthStore.Begin(); using var business = BusinessReadProof.Begin(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)); using var metadata = GoogleMetadataReadMemo.Begin(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)); await next(); });
if (connectedCache) app.UseConnectedCache();
app.UseTask005SessionAuthentication();
if (connectedCache) app.UseConnectedWriteGate();
app.Use(async (context, next) =>
{
    var mustChange = context.User.Identity?.IsAuthenticated == true &&
        bool.TryParse(
            context.User.FindFirst(OpaqueSessionAuthenticationHandler.MustChangePasswordClaim)?.Value,
            out var required) && required;
    var path = context.Request.Path;
    var allowed = path.StartsWithSegments("/Account/ChangePassword") ||
        path.StartsWithSegments("/Account/Logout") ||
        path.StartsWithSegments("/api/auth/change-password") ||
        path.StartsWithSegments("/api/auth/logout") ||
        path.StartsWithSegments("/api/me") ||
        path.StartsWithSegments("/css") ||
        path.StartsWithSegments("/js") ||
        path.StartsWithSegments("/lib");
    if (mustChange && !allowed)
    {
        if (path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Phải đổi mật khẩu trước khi dùng chức năng khác."
            });
        }
        else
        {
            context.Response.Redirect("/Account/ChangePassword");
        }

        return;
    }

    await next();
});
app.UseAuthorization();
app.MapPublicUiAssets();
app.MapRazorPages().WithStaticAssets();
app.MapTask005AuthApi();
app.MapTask006CategoryApi();
app.MapTask007AssetApi();
app.MapTask008TreeApi();
app.MapTask009MediaApi();
app.MapTask010InventoryApi();
app.MapTask011ReportApi();
if (connectedCache) app.MapConnectedCache();

if (localOffline)
{
    var workspace = app.Services.GetRequiredService<DuyTien.AssetManagement.Infrastructure.Local.LocalWorkspace>();
    app.MapGet("/api/system/status", (HttpContext context) => { context.Response.Headers.CacheControl = "no-store"; return Results.Ok(new { state = "LOCAL_OFFLINE", mode = "LocalOffline", workspaceId = workspace.WorkspaceId, singleWriterMode = true, requiresGoogle = false, outboundAttempts = OfflineOutboundGuard.Attempts }); });
}
else app.MapLocalSystemStatus();

await using var authWriterLease = await app.Services
    .GetRequiredService<IAuthWriterLeaseManager>()
    .AcquireAsync("web-server");
await app.RunAsync();
