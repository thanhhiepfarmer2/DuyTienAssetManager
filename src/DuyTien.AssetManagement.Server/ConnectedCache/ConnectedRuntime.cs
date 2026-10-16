using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net;
using System.Text.RegularExpressions;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Concurrency;
using DuyTien.AssetManagement.Server.Authentication;
using Microsoft.AspNetCore.Antiforgery;

namespace DuyTien.AssetManagement.Server.ConnectedCache;

public sealed class ConnectedRuntime(ProtectedQueryCache cache, ConnectionGate connection, IAuthSystemStore auth, TimeProvider clock) : IDisposable
{
    private volatile bool serviceUnavailable;
    public const string ValidationStartedItem = "DuyTien.ValidationStarted.v2";
    private const string RequestEpochItem = "DuyTien.RequestEpoch.v2";
    private const string IntentItem = "DuyTien.WriteIntent.v2";
    private const string StartedItem = "DuyTien.BusinessStarted.v2";
    private const string ProofItem = "DuyTien.CentralProof.v2";
    public static string? Credential(HttpContext c) => c.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? c.Request.Headers.Authorization.ToString()[7..].Trim() : c.Request.Cookies[OpaqueSessionAuthenticationHandler.CookieName];
    public async Task RememberAsync(HttpContext c, string token, AuthenticatedSession session)
    {
        var proof = await auth.ReadAsync(s =>
        {
            var now = clock.GetUtcNow(); var user = s.Users.Single(u => u.UserId == session.UserId);
            var row = s.Sessions.Single(t => t.SessionId == session.SessionId);
            if (user.MustChangePassword || row.RevokedAt is not null || user.State != AuthUserState.Active || user.SecurityVersion != row.SecurityVersion) return null;
            var assignments = s.RoleAssignments.Where(a => a.UserId == user.UserId && AuthAuthorizationRules.IsEffective(a, now)).OrderBy(a => a.AssignmentId).ToArray();
            var scopes = s.RoleAssignmentScopes.Where(g => assignments.Any(a => a.AssignmentId == g.AssignmentId)).OrderBy(g => g.ScopeGrantId).ToArray();
            var expires = new[] { row.IdleExpiresAt, row.AbsoluteExpiresAt }.Concat(assignments.Where(a => a.EffectiveTo.HasValue).Select(a => a.EffectiveTo!.Value)).Min();
            return new CentralProof(user.UserId, row.SessionId, user.Username, user.DisplayName, session.Roles.ToArray(), user.SecurityVersion,
                ProtectedQueryCache.Hash(JsonSerializer.Serialize(new { user.SecurityVersion, user.State, user.MustChangePassword, assignments, scopes })), c.Items[ValidationStartedItem] is DateTimeOffset started ? started : now, expires);
        }, c.RequestAborted);
        if (proof is not null) { await cache.SaveProofAsync(token, proof, c.RequestAborted, c.Items[RequestEpochItem] as string); c.Items[ProofItem] = proof; }
    }
    private readonly SemaphoreSlim mutationBoundary = new(1, 1);
    public async Task InvokeAsync(HttpContext c, RequestDelegate next)
    {
        using var operation = ConnectionGate.BeginOperation();
        var mutation = !HttpMethods.IsGet(c.Request.Method) && !HttpMethods.IsHead(c.Request.Method) && !HttpMethods.IsOptions(c.Request.Method);
        if (mutation) await mutationBoundary.WaitAsync(c.RequestAborted);
        try { await InvokeCoreAsync(c, next); }
        catch (OperationCanceledException) when (c.RequestAborted.IsCancellationRequested) { throw; }
        catch (BadHttpRequestException exception) when (!c.Response.HasStarted)
        { c.Response.Clear(); await Error(c, exception.StatusCode, "REQUEST_INVALID", exception.StatusCode == 413 ? "Yêu cầu vượt giới hạn kích thước; chưa gửi thao tác ghi." : "Yêu cầu không hợp lệ; chưa xác nhận thay đổi."); }
        catch (Exception exception) when (!c.Response.HasStarted)
        {
            serviceUnavailable = true;
            c.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ConnectedCache").LogWarning("Connected request failed with {ExceptionType}; no request/credential recorded.", exception.GetType().Name);
            c.Response.Clear(); await Error(c, 503, "SERVICE_UNAVAILABLE", "Dịch vụ/cache chưa đáp ứng. Giữ form/key; chưa xác nhận thao tác ghi thành công.");
        }
        finally { if (mutation) mutationBoundary.Release(); }
    }
    private async Task InvokeCoreAsync(HttpContext c, RequestDelegate next)
    {
        var path = c.Request.Path.Value ?? "/";
        // Status is technical and does not run the central auth pipeline.
        var internet = await connection.ObserveAsync(c.RequestAborted);
        c.Items[RequestEpochItem] = connection.Epoch;
        ConnectionGate.BindOperation(connection.Epoch);
        var token = Credential(c); var ticket = cache.Read(token); var valid = ProtectedQueryCache.Valid(ticket, clock.GetUtcNow());
        if (path == "/api/connection")
        {
            c.Response.Headers.CacheControl = "no-store";
            await c.Response.WriteAsJsonAsync(new { mode = "Connected", state = State(internet, valid ? ticket : null), lastSyncedAt = valid ? ticket!.Views.Select(v => (DateTimeOffset?)v.AsOf).Max() : null,
                hasOfflineSession = valid, viewerId = valid ? ticket!.Proof.UserId : null, sessionId = valid ? ticket!.Proof.SessionId : null, authorityHash = valid ? ticket!.Proof.AuthorityHash : null, cacheId = valid ? cache.CentralId : null, uiCacheRevision = Presentation.WebUi.CacheRevision, outdatedHtmlViews = valid ? ticket!.Views.Count(v => !Presentation.CachedHtmlPresentation.IsCurrent(v)) : 0, cachedViews = valid ? ticket!.Views.Length : 0, pendingWrite = valid && ticket!.Pending is not null, writeRetryPermitted = valid && ticket!.Pending?.JournalConfirmed == true, refreshPath = valid && ticket!.Proof.Roles.Contains("CatalogManager") ? "/Categories" : valid && ticket!.Proof.Roles.Contains("DocumentManager") && !ticket.Proof.Roles.Contains("Admin") ? "/Documents" : "/Assets", nextProbeLimited = true }, c.RequestAborted); return;
        }
        if (path == "/api/connection/files")
        {
            c.Response.Headers.CacheControl = "no-store";
            if (!valid) { await Error(c, 401, "ONLINE_LOGIN_REQUIRED", "Cần phiên online/cache hợp lệ."); return; }
            await c.Response.WriteAsJsonAsync(new { partial = true, files = ticket!.Views.Where(v => FileView(v.Key)).Select(v => new { url = v.Key[..v.Key.LastIndexOf('|')], offlineAvailable = cache.Bytes(token!, v) is not null, asOf = v.AsOf }) }); return;
        }
        if (path is "/Account/LocalSetup" or "/Admin/LocalDemo") { c.Response.StatusCode = 410; await c.Response.WriteAsync("LocalOffline ghi độc lập đã ngừng. Đăng nhập tài khoản trung tâm tại /Account/Login."); return; }
        if (path == "/api/system/status") { c.Response.Headers.CacheControl = "no-store"; await c.Response.WriteAsJsonAsync(new { mode = "Connected", state = State(internet, valid ? ticket : null), singleWriterMode = true, buildId = typeof(ConnectedRuntime).Assembly.ManifestModule.ModuleVersionId, environment = c.RequestServices.GetRequiredService<IHostEnvironment>().EnvironmentName, storage = c.RequestServices.GetRequiredService<IConfiguration>()["Storage:Mode"] ?? "Google" }); return; }
        var writing = !HttpMethods.IsGet(c.Request.Method) && !HttpMethods.IsHead(c.Request.Method) && !HttpMethods.IsOptions(c.Request.Method);
        if (path is "/Account/Logout" or "/api/auth/logout" && HttpMethods.IsPost(c.Request.Method) && internet != InternetState.Connected)
        {
            if (ticket is null) { await Error(c, 401, "ONLINE_LOGIN_REQUIRED", "Chưa có phiên trên máy để thu hồi."); return; }
            // An expired proof may revoke only its own local capability; it never authorizes business access.
            c.User = Principal(ticket, "cookie");
            try { if (!c.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c); }
            catch (AntiforgeryValidationException) { await Error(c, 400, "ANTIFORGERY", "Yêu cầu đăng xuất không hợp lệ."); return; }
            await cache.RevokeAsync(token!, c.RequestAborted); c.Response.Cookies.Delete(OpaqueSessionAuthenticationHandler.CookieName); if (path.StartsWith("/api/")) c.Response.StatusCode = 204; else c.Response.Redirect("/Account/Login"); return;
        }
        if (internet != InternetState.Connected)
        {
            if (writing) { await Error(c, 423, "READ_ONLY", "Ngoại tuyến/chưa xác minh mạng — chỉ đọc. Không gửi thay đổi tới trung tâm."); return; }
            if (internet != InternetState.Disconnected) { await Error(c, 503, "CONNECTION_UNCONFIRMED", "Chưa xác định được kết nối; chưa được phép dùng cache offline."); return; }
            if (!valid) { await Error(c, 401, "ONLINE_LOGIN_REQUIRED", "Chưa có phiên online/cache hợp lệ hoặc phiên đã hết hạn. Cần kết nối và đăng nhập online."); return; }
            c.User = Principal(ticket!, "cookie");
            if (path == "/Account/Login") { c.Response.Redirect("/Offline"); return; }
            if (path == "/Offline") { await Index(c, token!, ticket!); return; }
            var view = ticket!.Views.SingleOrDefault(v => v.Key == ViewKey(c)); var bytes = view is null ? null : cache.Bytes(token!, view);
            if (!Readable(path) || view is null || bytes is null) { await Error(c, 503, "OFFLINE_CACHE_MISSING", "Ngoại tuyến — chỉ đọc. Trang/query hoặc bytes file này chưa được lưu trên máy. Mở /Offline để tìm trong dữ liệu đã có."); return; }
            c.Response.StatusCode = 200; c.Response.ContentType = view.ContentType; c.Response.Headers.CacheControl = "no-store";
            c.Response.Headers["X-Content-Type-Options"] = "nosniff";
            c.Response.Headers["X-DuyTien-Files"] = "PARTIAL_CHECK_OFFLINE_AVAILABILITY";
            c.Response.Headers["X-DuyTien-State"] = "OFFLINE_READ_ONLY"; c.Response.Headers["X-DuyTien-AsOf"] = view.AsOf.ToString("O");
            if (view.Disposition is not null) c.Response.Headers.ContentDisposition = view.Disposition;
            if (Presentation.CachedHtmlPresentation.IsHtml(view.ContentType))
            {
                c.Response.Headers["X-DuyTien-Ui-Revision"] = Presentation.CachedHtmlPresentation.IsCurrent(view) ? Presentation.WebUi.CacheRevision : "LEGACY_READ_ONLY";
                if (!Presentation.CachedHtmlPresentation.IsCurrent(view)) { await c.Response.WriteAsync(Presentation.CachedHtmlPresentation.Legacy(bytes, view.AsOf, ticket.Views.Where(v => FileView(v.Key) && cache.Bytes(token!, v) is not null).Select(v => v.Key[..v.Key.LastIndexOf('|')])), c.RequestAborted); return; }
                var banner = $"<aside role=\"status\" style=\"padding:12px;background:#fff3cd\">Ngoại tuyến — chỉ đọc · Đồng bộ trang này: {WebUtility.HtmlEncode(view.AsOf.ToString("O"))}. Cache có thể thiếu dữ liệu/file. <a href=\"/Offline\">Tìm dữ liệu đã lưu</a></aside>";
                var html = Encoding.UTF8.GetString(bytes); html = Regex.Replace(html, "<body[^>]*>", m => m.Value.Insert(m.Value.Length - 1, " data-connection-state=\"OFFLINE_READ_ONLY\"") + banner, RegexOptions.IgnoreCase);
                await c.Response.WriteAsync(html, c.RequestAborted);
            }
            else await c.Response.Body.WriteAsync(bytes, c.RequestAborted);
            return;
        }
        if (ticket?.Revoked == true && !path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase) && !path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)) { c.Response.Cookies.Delete(OpaqueSessionAuthenticationHandler.CookieName); await Error(c, 401, "ONLINE_LOGIN_REQUIRED", "Phiên đã đăng xuất/thu hồi trên máy này. Cần đăng nhập online lại."); return; }
        if (writing && valid && path is "/Account/Logout" or "/api/auth/logout")
        {
            if (!c.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                c.User = Principal(ticket!, "cookie");
                try { await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c); }
                catch (AntiforgeryValidationException) { await Error(c, 400, "ANTIFORGERY", "Yêu cầu đăng xuất không hợp lệ."); return; }
            }
            await cache.RevokeAsync(token!, c.RequestAborted);
            c.Response.Cookies.Delete(OpaqueSessionAuthenticationHandler.CookieName);
        }
        if (path == "/Offline") { c.Response.Redirect("/"); return; }
        var authWrite = path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase);
        if (writing && !authWrite && path != "/api/connection/reconcile" && (!valid || ticket!.ReadyEpoch != connection.Epoch))
        { await Error(c, 409, "RECONNECT_REQUIRED", "Phải xác minh phiên/quyền và tải dữ liệu mới từ trung tâm trước khi ghi."); return; }
        PendingWrite? intent = null;
        if (writing && !authWrite && path != "/api/connection/reconcile")
        {
            intent = await Intent(c);
            if (ticket!.Pending is { } pending && (!pending.JournalConfirmed || pending.Method != intent.Method || pending.Path != intent.Path || pending.Key != intent.Key || pending.BodyHash != intent.BodyHash))
            { await Error(c, 409, "WRITE_OUTCOME_UNKNOWN", "Kết quả ghi trước chưa rõ. Giữ form/key; gọi đối soát online trước khi retry cùng key/payload. Không gửi lại bằng key mới."); return; }
            c.Items[IntentItem] = intent;
        }
        // Cached responses never satisfy ONLINE requests. Existing services still validate current authority/data.
        var original = c.Response.Body; using var recording = new RecordingStream(64 * 1024 * 1024); c.Response.Body = recording;
        try
        {
            await next(c);
            if (HttpMethods.IsGet(c.Request.Method) && c.Response.StatusCode == 200 && (BusinessReadProof.Mixed || !Equals(c.Items[RequestEpochItem], connection.Epoch)))
            { c.Response.Clear(); recording.SetLength(0); if (token is not null) await cache.ObserveChangesAsync(token, BusinessReadProof.Values, CancellationToken.None); await Error(c, 409, "READ_SNAPSHOT_CHANGED", "Dữ liệu đổi trong khi đọc. Cần tải lại snapshot nhất quán, chưa được ghi."); }
            if (c.Response.StatusCode >= 500)
            {
                serviceUnavailable = true;
                await connection.CheckAsync(CancellationToken.None, force: true);
                if (token is not null) await cache.NotReadyAsync(token, CancellationToken.None);
            }
            if (c.User.Identity?.IsAuthenticated == true && token is not null && c.Items[ProofItem] is CentralProof proof)
            {
                if (writing && intent is not null && c.Items.ContainsKey(StartedItem) && c.Response.StatusCode < 500 && c.Response.StatusCode != 408) await cache.UpdatePendingAsync(token, null, CancellationToken.None);
                if (HttpMethods.IsGet(c.Request.Method) && c.Response.StatusCode == 200 && Readable(path) && recording.Complete)
                {
                    if (path != "/") serviceUnavailable = false;
                    await cache.SaveViewAsync(token, proof, path == "/" || !Equals(c.Items[RequestEpochItem], connection.Epoch) || connection.State != InternetState.Connected ? cache.Read(token)?.ReadyEpoch ?? "" : connection.Epoch, ViewKey(c), recording.Bytes, c.Response.ContentType ?? "application/octet-stream", c.Response.Headers.ContentDisposition.FirstOrDefault(), clock.GetUtcNow(), CancellationToken.None, BusinessReadProof.Values, c.Items[RequestEpochItem] as string);
                }
                if (path == "/Account/Logout" && writing && c.Response.StatusCode < 400 || path == "/api/auth/logout" && writing && c.Response.StatusCode < 400) await cache.RevokeAsync(token, CancellationToken.None);
            }
            else if (token is not null && (c.Response.StatusCode == 401 || c.User.Identity?.IsAuthenticated != true && !c.Items.ContainsKey(OpaqueSessionAuthenticationHandler.StorageUnavailableItem))) await cache.RevokeAsync(token, CancellationToken.None);
        }
        finally { c.Response.Body = original; }
        if (c.Response.StatusCode == 200 && Readable(path) && path != "/" && c.Items[ProofItem] is CentralProof verified)
        { c.Response.Headers["X-DuyTien-State"] = Equals(c.Items[RequestEpochItem], connection.Epoch) && connection.State == InternetState.Connected ? "ONLINE" : "RECONNECTING"; c.Response.Headers["X-DuyTien-VerifiedAt"] = verified.VerifiedAt.ToString("O"); }
        await original.WriteAsync(recording.Bytes, c.RequestAborted);
    }
    public async Task WriteAuthorityAsync(HttpContext c, RequestDelegate next)
    {
        var path = c.Request.Path.Value ?? "/";
        if (!HttpMethods.IsGet(c.Request.Method) && !HttpMethods.IsHead(c.Request.Method) && !HttpMethods.IsOptions(c.Request.Method) && !path.StartsWith("/Account/") && !path.StartsWith("/api/auth/") && path != "/api/connection/reconcile")
        {
            var current = cache.Read(Credential(c));
            if (connection.State != InternetState.Connected || !Equals(c.Items[RequestEpochItem], connection.Epoch) || !ProtectedQueryCache.Valid(current, clock.GetUtcNow()) || current!.ReadyEpoch != connection.Epoch || c.User.Identity?.IsAuthenticated != true)
            { await Error(c, 409, "RECONNECT_REQUIRED", "Phiên/quyền đã thay đổi. Tải dữ liệu mới từ trung tâm trước khi ghi."); return; }
            if (c.Items[IntentItem] is PendingWrite intent)
            {
                await cache.UpdatePendingAsync(Credential(c)!, intent, c.RequestAborted);
                c.Items[StartedItem] = true;
            }
        }
        await next(c);
    }
    public void Dispose() => mutationBoundary.Dispose();
    public string State(InternetState internet, CachedTicket? ticket) => internet == InternetState.Disconnected ? "OFFLINE_READ_ONLY" : internet != InternetState.Connected || serviceUnavailable ? "SERVICE_UNAVAILABLE" : ticket is not null && ticket.ReadyEpoch == connection.Epoch ? "ONLINE" : "RECONNECTING";
    private static bool FileView(string key)
    {
        var path = key.Split('|')[0].Split('?')[0];
        return path.StartsWith("/Files/Content/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/content", StringComparison.OrdinalIgnoreCase);
    }
    public static bool Readable(string path)
    {
        if (path == "/") return true;
        if (new[] { "/Create", "/Edit", "/Import", "/Move", "/Deactivate", "/History" }.Any(x => path.Contains(x, StringComparison.OrdinalIgnoreCase))) return false;
        return new[] { "/Categories", "/Assets", "/Documents", "/Items", "/Stock", "/Reports", "/api/categories", "/api/assets", "/api/documents", "/api/items", "/api/stock", "/api/reports", "/api/files", "/Files/Content" }.Any(x => path.Equals(x, StringComparison.OrdinalIgnoreCase) || path.StartsWith(x + "/", StringComparison.OrdinalIgnoreCase));
    }
    private static string ViewKey(HttpContext c) => c.Request.PathBase + c.Request.Path + c.Request.QueryString + (c.Request.Path.StartsWithSegments("/api") ? "|json" : "|html");
    private static ClaimsPrincipal Principal(CachedTicket t, string source)
    {
        var p = t.Proof; var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, p.UserId), new(ClaimTypes.Name, p.Username), new("display_name", p.DisplayName), new("session_id", p.SessionId), new(OpaqueSessionAuthenticationHandler.CredentialSourceClaim, source), new(OpaqueSessionAuthenticationHandler.MustChangePasswordClaim, "False") };
        claims.AddRange(p.Roles.Select(r => new Claim(ClaimTypes.Role, r))); return new(new ClaimsIdentity(claims, OpaqueSessionAuthenticationHandler.SchemeName));
    }
    private static async Task<PendingWrite> Intent(HttpContext c)
    {
        if (c.Request.ContentLength > 28 * 1024 * 1024) throw new BadHttpRequestException("Request too large", 413);
        string key = c.Request.Headers["Idempotency-Key"].ToString(), hash;
        if (c.Request.HasFormContentType)
        {
            var form = await c.Request.ReadFormAsync(c.RequestAborted); if (key == "") key = form["RequestKey"].ToString(); if (key == "") key = form["Key"].ToString();
            var values = form.Where(p => p.Key != "__RequestVerificationToken").OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value).ToList();
            foreach (var file in form.Files) { using var stream = file.OpenReadStream(); values.Add(file.Name + "|" + file.FileName + "|" + Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream, c.RequestAborted))); }
            hash = ProtectedQueryCache.Hash(string.Join("\n", values));
        }
        else
        {
            c.Request.EnableBuffering(65536, 28 * 1024 * 1024); hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(c.Request.Body, c.RequestAborted)); c.Request.Body.Position = 0;
        }
        if (key.Length > 256) key = "";
        return new(c.Request.Method, c.Request.Path + c.Request.QueryString, key, hash);
    }
    private async Task Index(HttpContext c, string token, CachedTicket ticket)
    {
        var q = c.Request.Query["q"].ToString(); var kind = c.Request.Query["kind"].ToString(); var rows = new List<object>();
        foreach (var view in ticket.Views)
        {
            if (kind != "" && !view.Key.StartsWith("/" + kind, StringComparison.OrdinalIgnoreCase) && !view.Key.StartsWith("/api/" + kind, StringComparison.OrdinalIgnoreCase)) continue;
            var bytes = cache.Bytes(token, view); if (bytes is null || !(view.ContentType.Contains("json") || view.ContentType.Contains("html"))) continue;
            var text = cache.SearchText(token, view);
            if (q != "" && !text.Contains(ProtectedQueryCache.Normalize(q), StringComparison.Ordinal)) continue;
            rows.Add(new { url = view.Key[..view.Key.LastIndexOf('|')], asOf = view.AsOf, excerpt = text[..Math.Min(text.Length, 250)] });
        }
        var page = int.TryParse(c.Request.Query["page"], out var parsed) ? Math.Clamp(parsed, 1, 100000) : 1;
        var matches = rows.Skip((page - 1) * 60).Take(60).ToArray();
        c.Response.Headers.CacheControl = "no-store";
        if (c.Request.Headers.Accept.ToString().Contains("application/json")) { await c.Response.WriteAsJsonAsync(new { state = "OFFLINE_READ_ONLY", total = rows.Count, results = matches, page, partial = true }); return; }
        c.Response.ContentType = "text/html; charset=utf-8";
        var tokens = c.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(c);
        var html = new StringBuilder("<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><link rel=\"stylesheet\" href=\"/css/site.css\"><title>Dữ liệu ngoại tuyến · Duy Tiến Assets</title><body class=\"offline-page\"><h1>Ngoại tuyến — chỉ đọc</h1><p>Chỉ trang và truy vấn đã lưu; có thể thiếu dữ liệu hoặc tệp. Mỗi bản lưu có thời điểm riêng.</p><form method=\"get\"><label>Tìm trong bản lưu <input name=\"q\" placeholder=\"Tìm trong dữ liệu đã lưu\"></label><label>Chức năng <select name=\"kind\"><option value=\"\">Mọi chức năng</option><option value=\"Assets\">Tài sản</option><option value=\"Categories\">Danh mục</option><option value=\"Items\">Vật tư, hàng hóa</option><option value=\"Documents\">Hồ sơ</option><option value=\"Reports\">Báo cáo</option><option value=\"Stock\">Số lượng</option></select></label><button>Tìm/lọc</button></form>");
        html.Replace("name=\"q\" placeholder", "name=\"q\" value=\"" + WebUtility.HtmlEncode(q) + "\" placeholder");
        if (kind != "") html.Replace("value=\"" + WebUtility.HtmlEncode(kind) + "\">", "value=\"" + WebUtility.HtmlEncode(kind) + "\" selected>");
        if (page > 1) html.Append("<p><a href=\"/Offline?page=" + (page - 1) + "&amp;q=" + WebUtility.HtmlEncode(Uri.EscapeDataString(q)) + "&amp;kind=" + WebUtility.HtmlEncode(Uri.EscapeDataString(kind)) + "\">Trang trước</a></p>");
        html.Append("<p>").Append(rows.Count).Append(" bản lưu khớp; hiển thị tối đa 60.</p>");
        foreach (var row in matches) { var j = JsonSerializer.SerializeToElement(row); html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(j.GetProperty("url").GetString())).Append("\">").Append(WebUtility.HtmlEncode(j.GetProperty("url").GetString())).Append("</a> · ").Append(WebUtility.HtmlEncode(j.GetProperty("asOf").ToString())).Append("</p>"); }
        if (rows.Count > page * 60) html.Append("<p><a href=\"/Offline?page=" + (page + 1) + "&amp;q=" + WebUtility.HtmlEncode(Uri.EscapeDataString(q)) + "&amp;kind=" + WebUtility.HtmlEncode(Uri.EscapeDataString(kind)) + "\">Trang tiếp</a></p>");
        html.Append("<form method=\"post\" action=\"/Account/Logout\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"").Append(WebUtility.HtmlEncode(tokens.RequestToken)).Append("\"><button>Đăng xuất trên máy này</button></form><script src=\"/js/connection.js\"></script></body></html>");
        await c.Response.WriteAsync(html.ToString());
    }
    private static async Task Error(HttpContext c, int status, string code, string message)
    {
        c.Response.StatusCode = status; c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-DuyTien-State"] = code;
        if (c.Request.Path.StartsWithSegments("/api")) await c.Response.WriteAsJsonAsync(new { status, code, title = message });
        else { c.Response.ContentType = "text/html; charset=utf-8"; await c.Response.WriteAsync("<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><link rel=\"stylesheet\" href=\"/css/site.css\"><body class=\"offline-page\"><h1>" + WebUtility.HtmlEncode(message) + "</h1><a href=\"/Offline\">Dữ liệu đã lưu</a> · <a href=\"/Account/Login\">Đăng nhập online</a><script src=\"/js/connection.js\"></script></body></html>"); }
    }
}
internal sealed class RecordingStream(int limit) : MemoryStream
{
    public bool Complete => true; public byte[] Bytes => ToArray();
    private void Check(int size) { if (Position + size > limit) throw new IOException("Response vượt giới hạn buffer an toàn, không cache partial."); }
    public override void Write(byte[] b, int o, int n) { Check(n); base.Write(b, o, n); }
    public override void Write(ReadOnlySpan<byte> b) { Check(b.Length); base.Write(b); }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> b, CancellationToken ct = default) { Check(b.Length); return base.WriteAsync(b, ct); }
    public override Task WriteAsync(byte[] b, int o, int n, CancellationToken ct) { Check(n); return base.WriteAsync(b, o, n, ct); }
}
