using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DuyTien.AssetManagement.Mobile.Client;

// Serialize requests with rotation/logout. A refresh token is used at most once per attempted rotation.
public sealed class MobileSession(HttpClient http, Uri endpoint, IMobileSecretStore store, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Uri backend = MobileEndpoint.Parse(endpoint.AbsoluteUri);
    private MobileTokens? tokens;
    public bool IsSignedIn => tokens is not null;
    public bool MustChangePassword => tokens?.MustChangePassword == true;
    public event Action? SessionEnded;

    public async Task<bool> RestoreAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            SessionEnvelope? saved;
            try { var raw = await store.ReadAsync(); saved = raw is null ? null : JsonSerializer.Deserialize<SessionEnvelope>(raw, MobileJson.Options); }
            catch { await ClearAsync(); return false; }
            if (saved is null) return false;
            if (saved.RefreshInFlight || saved.Backend != backend.AbsoluteUri || !Valid(saved.Tokens) || saved.Tokens.RefreshExpiresAt <= clock.GetUtcNow())
            { await ClearAsync(); return false; }
            tokens = saved.Tokens;
            return true; // Caller must read /api/me before displaying any saved authority/data.
        }
        finally { gate.Release(); }
    }
    public async Task LoginAsync(string username, string password, CancellationToken ct = default)
    {
        using var deadline = Deadline(ct); ct = deadline.Token;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new MobileApiException(400, "Nhập tên đăng nhập và mật khẩu.");
        await gate.WaitAsync(ct);
        try
        {
            if (tokens is not null) throw new MobileApiException(409, "Đăng xuất trước khi đổi tài khoản.");
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(backend, "api/auth/login"))
            { Content = JsonContent.Create(new { username, password }) };
            using var response = await TransportAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw await ErrorAsync(response, true, ct);
            var received = await ReadJsonAsync<MobileTokens>(response, ct);
            if (!Valid(received)) throw new MobileApiException(503, "Backend trả phiên không hợp lệ; chưa đăng nhập.");
            tokens = received;
            try { await PersistAsync(false); } catch { await ClearAsync(); throw new MobileApiException(0, "Không lưu được phiên vào vùng bảo mật. Đăng nhập lại."); }
        }
        finally { gate.Release(); }
    }
    public async Task<T> JsonAsync<T>(Func<HttpRequestMessage> create, CancellationToken ct = default)
    {
        using var deadline = Deadline(ct); ct = deadline.Token;
        await gate.WaitAsync(ct);
        try { using var response = await AuthorizedAsync(create, ct); return await ReadJsonAsync<T>(response, ct); }
        finally { gate.Release(); }
    }
    public async Task<byte[]> PhotoAsync(string fileId, CancellationToken ct = default)
    {
        using var deadline = Deadline(ct); ct = deadline.Token;
        await gate.WaitAsync(ct);
        try
        {
            using var response = await AuthorizedAsync(() => new(HttpMethod.Get, "api/files/" + Uri.EscapeDataString(fileId) + "/content?preview=true"), ct);
            var bytes = await ReadBytesAsync(response, PhotoSelection.MaxBytes, ct);
            PhotoSelection.Validate(bytes);
            return bytes;
        }
        finally { gate.Release(); }
    }
    public async Task ChangePasswordAsync(string current, string next, CancellationToken ct = default)
    {
        using var deadline = Deadline(ct); ct = deadline.Token;
        if (next.Length < 8) throw new MobileApiException(400, "Mật khẩu mới tối thiểu 8 ký tự.");
        await gate.WaitAsync(ct);
        try
        {
            using var response = await AuthorizedAsync(() => new(HttpMethod.Post, "api/auth/change-password")
            { Content = JsonContent.Create(new { currentPassword = current, newPassword = next }) }, ct);
            await ClearAsync(); // Server revokes the old sessions after password change.
        }
        finally { gate.Release(); }
    }
    public async Task LogoutAsync(CancellationToken ct = default)
    {
        using var deadline = Deadline(ct); ct = deadline.Token;
        await gate.WaitAsync(ct);
        try
        {
            if (tokens is not null)
            { using var response = await AuthorizedAsync(() => new(HttpMethod.Post, "api/auth/logout"), ct); }
        }
        finally { try { await ClearAsync(); } finally { gate.Release(); } }
    }
    private async Task<HttpResponseMessage> AuthorizedAsync(Func<HttpRequestMessage> create, CancellationToken ct)
    {
        if (tokens is null) throw new MobileApiException(401, "Phiên đã hết hạn hoặc bị thu hồi. Đăng nhập lại.");
        using var probe = create();
        var path = probe.RequestUri!.OriginalString;
        if (tokens.MustChangePassword && path != "api/me" && path != "api/auth/change-password" && path != "api/auth/logout")
            throw new MobileApiException(403, "Cần đổi mật khẩu trước khi sử dụng dữ liệu.");
        if (tokens.AccessExpiresAt <= clock.GetUtcNow()) await RefreshAsync(ct);
        var attempted = tokens!.AccessToken;
        var response = await SendBearerAsync(create, attempted, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            await RefreshAsync(ct);
            response = await SendBearerAsync(create, tokens!.AccessToken, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            { response.Dispose(); await ClearAsync(); throw new MobileApiException(401, "Phiên đã bị thu hồi. Đăng nhập lại."); }
        }
        if (!response.IsSuccessStatusCode)
        { try { throw await ErrorAsync(response, path.StartsWith("api/auth/", StringComparison.Ordinal), ct); } finally { response.Dispose(); } }
        return response;
    }
    private async Task RefreshAsync(CancellationToken ct)
    {
        if (tokens is null || tokens.RefreshExpiresAt <= clock.GetUtcNow())
        { await ClearAsync(); throw new MobileApiException(401, "Phiên hết hạn. Đăng nhập lại."); }
        var previous = tokens;
        try
        {
            await PersistAsync(true); // Crash/lost response must never replay the previous token.
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(backend, "api/auth/refresh"))
            { Content = JsonContent.Create(new { refreshToken = previous.RefreshToken }) };
            using var response = await TransportAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new MobileApiException(401, "Phiên không còn hiệu lực.");
            var rotated = await ReadJsonAsync<MobileTokens>(response, ct);
            if (!Valid(rotated) || rotated.RefreshToken == previous.RefreshToken)
                throw new MobileApiException(401, "Không xác nhận được phiên mới.");
            tokens = new MobileTokens
            {
                AccessToken = rotated.AccessToken,
                RefreshToken = rotated.RefreshToken,
                AccessExpiresAt = rotated.AccessExpiresAt,
                RefreshExpiresAt = rotated.RefreshExpiresAt,
                MustChangePassword = previous.MustChangePassword
            };
            await PersistAsync(false);
        }
        catch
        { await ClearAsync(); throw new MobileApiException(401, "Không xác nhận được refresh; đăng nhập lại để tránh dùng lại token cũ."); }
    }
    private async Task<HttpResponseMessage> SendBearerAsync(Func<HttpRequestMessage> create, string token, CancellationToken ct)
    {
        using var request = create();
        var relative = request.RequestUri!;
        if (relative.IsAbsoluteUri || !relative.OriginalString.StartsWith("api/", StringComparison.Ordinal))
            throw new MobileApiException(400, "Route client không hợp lệ.");
        request.RequestUri = new Uri(backend, relative);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await TransportAsync(request, ct);
    }
    private async Task<HttpResponseMessage> TransportAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try { return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        { throw new MobileApiException(0, "Mất kết nối hoặc quá thời gian. Nội dung vẫn giữ; thử lại cùng yêu cầu."); }
    }
    private static CancellationTokenSource Deadline(CancellationToken ct)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        source.CancelAfter(TimeSpan.FromSeconds(60)); // Covers response-body reads as well as headers.
        return source;
    }
    private Task PersistAsync(bool pending) => store.WriteAsync(JsonSerializer.Serialize(new SessionEnvelope
    { Backend = backend.AbsoluteUri, Tokens = tokens!, RefreshInFlight = pending }, MobileJson.Options));
    private async Task ClearAsync()
    {
        tokens = null;
        SessionEnded?.Invoke();
        try { await store.RemoveAsync(); }
        catch { throw new MobileApiException(0, "Không xóa được phiên trong vùng bảo mật. Không tiếp tục dùng phiên; kiểm tra lưu trữ ứng dụng."); }
    }
    private static bool Valid(MobileTokens t) => !string.IsNullOrWhiteSpace(t.AccessToken) && !string.IsNullOrWhiteSpace(t.RefreshToken)
        && t.AccessExpiresAt != default && t.RefreshExpiresAt != default;
    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(await ReadBytesAsync(response, 2 * 1024 * 1024, ct), MobileJson.Options)
            ?? throw new JsonException();
        }
        catch (JsonException) { throw new MobileApiException(503, "Phản hồi backend không đúng contract. Chưa xác nhận thành công."); }
    }
    private static async Task<byte[]> ReadBytesAsync(HttpResponseMessage response, int cap, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > cap) throw new MobileApiException(503, "Phản hồi vượt giới hạn client.");
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream(); var chunk = new byte[81920]; int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            { if (buffer.Length + read > cap) throw new MobileApiException(503, "Phản hồi vượt giới hạn client."); buffer.Write(chunk, 0, read); }
            return buffer.ToArray();
        }
        catch (Exception e) when (e is IOException or HttpRequestException or OperationCanceledException)
        { throw new MobileApiException(0, "Phản hồi bị gián đoạn. Giữ yêu cầu để thử lại."); }
    }
    private static async Task<MobileApiException> ErrorAsync(HttpResponseMessage r, bool auth, CancellationToken ct)
    {
        var status = (int)r.StatusCode;
        string message = status switch
        {
            400 or 422 => "Dữ liệu không hợp lệ. Kiểm tra các trường; nội dung vẫn giữ.",
            401 => "Tên đăng nhập/mật khẩu sai hoặc phiên không hợp lệ.",
            403 => "Không có quyền hoặc cần đổi mật khẩu.",
            404 => "Không tìm thấy dữ liệu hoặc ngoài phạm vi.",
            409 => "Version/cây hoặc yêu cầu đã thay đổi. Nạp bản mới trước khi ghi tiếp.",
            413 => "Ảnh vượt giới hạn backend.",
            429 => "Quá nhiều lần đăng nhập. Chờ rồi thử lại.",
            _ => "Dịch vụ chưa xác nhận thành công. Giữ nội dung và thử lại cùng yêu cầu."
        };
        string? fileId = null;
        if (!auth)
        {
            try
            {
                using var json = JsonDocument.Parse(await ReadBytesAsync(r, 65536, ct));
                if (json.RootElement.TryGetProperty("fileId", out var field) && field.ValueKind == JsonValueKind.String)
                { var value = field.GetString(); if (value?.StartsWith("FILE-", StringComparison.Ordinal) == true && value.Length <= 100) fileId = value; }
                // Only the backend's documented business validation title, never raw auth/upstream bodies.
                if (status is 400 or 422 && json.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    message = title.GetString() is { Length: > 0 and < 1000 } text ? text : message;
            }
            catch (Exception e) when (e is JsonException or MobileApiException) { }
        }
        return new(status, message, fileId);
    }
}
