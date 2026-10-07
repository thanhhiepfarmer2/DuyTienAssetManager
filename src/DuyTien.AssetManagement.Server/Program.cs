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
var configuredMinimumPasswordLength = builder.Configuration.GetValue<int?>(
    "Auth:Password:MinimumLength") ?? AuthPasswordPolicy.MinimumLength;
if (configuredMinimumPasswordLength != AuthPasswordPolicy.MinimumLength)
{
    throw new InvalidOperationException(
        "Auth:Password:MinimumLength must remain 8 for the accepted TASK-005 policy.");
}

builder.Services.AddRazorPages();
builder.Services.AddAuthentication(OpaqueSessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, OpaqueSessionAuthenticationHandler>(
        OpaqueSessionAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();
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
builder.Services.AddSingleton(serviceProvider =>
    new GoogleApiRequestExecutor(
        serviceProvider.GetRequiredService<GoogleWorkspaceOptions>(),
        serviceProvider.GetRequiredService<IGoogleAccessTokenProvider>(),
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleApi")));
builder.Services.AddSingleton<GoogleSheetsDataStore>();
builder.Services.AddSingleton<GoogleAuthSystemStore>();
builder.Services.AddSingleton<IAuthSystemStore>(serviceProvider =>
    serviceProvider.GetRequiredService<GoogleAuthSystemStore>());
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
builder.Services.AddSingleton<ISingleWriterCoordinator, InProcessSingleWriterCoordinator>();
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
builder.Services.AddSingleton<PocAssetWriteService>();
builder.Services.AddSingleton<PocFileWriteService>();
builder.Services.AddSingleton(serviceProvider =>
    new GoogleOAuthBootstrapper(
        serviceProvider.GetRequiredService<GoogleWorkspaceOptions>(),
        serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("GoogleOAuth")));

var app = builder.Build();

if (await GoogleWorkspaceCommandRunner.TryRunAsync(args, app.Services))
{
    return;
}

if (await AuthCommandRunner.TryRunAsync(args, app.Services))
{
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
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
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapTask005AuthApi();

app.MapGet(
    "/api/system/status",
    async (
        IAssetDataStore dataStore,
        IAssetFileStore fileStore,
        CancellationToken cancellationToken) =>
    {
        var sheets = await dataStore.CheckReadinessAsync(cancellationToken);
        var drive = await fileStore.CheckReadinessAsync(cancellationToken);

        return Results.Ok(new SystemStatusResponse(
            State: "TASK005_LOCAL_IN_PROGRESS",
            SingleWriterMode: true,
            Sheets: new ExternalStorageStatus(
                sheets.ConfigurationPresent,
                sheets.LiveConnectionVerified),
            Drive: new ExternalStorageStatus(
                drive.ConfigurationPresent,
                drive.LiveConnectionVerified)));
    });

await using var authWriterLease = await app.Services
    .GetRequiredService<IAuthWriterLeaseManager>()
    .AcquireAsync("web-server");
await app.RunAsync();
