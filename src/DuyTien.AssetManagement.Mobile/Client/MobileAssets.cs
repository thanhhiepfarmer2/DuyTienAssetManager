using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Media;

namespace DuyTien.AssetManagement.Mobile.Client;

public sealed class MobileAssets(MobileSession session)
{
    public Task<MobileIdentity> MeAsync(CancellationToken ct = default) => session.JsonAsync<MobileIdentity>(() => new(HttpMethod.Get, "api/me"), ct);
    public Task<AssetList> ListAsync(AssetQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new MobileApiException(400, "Phân trang phải từ1, pageSize1–100.");
        var values = new Dictionary<string, string?>
        {
            ["q"] = query.Q,
            ["categoryId"] = query.CategoryId,
            ["orgUnitId"] = query.OrgUnitId,
            ["locationId"] = query.LocationId,
            ["condition"] = query.Condition,
            ["custodianRef"] = query.CustodianRef,
            ["includeDescendants"] = query.IncludeDescendants.ToString(),
            ["includeDeleted"] = query.IncludeDeleted.ToString(),
            ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture)
        };
        var route = "api/assets?" + string.Join("&", values.Where(x => !string.IsNullOrEmpty(x.Value))
            .Select(x => x.Key + "=" + Uri.EscapeDataString(x.Value!)));
        return session.JsonAsync<AssetList>(() => new(HttpMethod.Get, route), ct);
    }
    public Task<AssetView> DetailAsync(string id, CancellationToken ct = default) => session.JsonAsync<AssetView>(() => new(HttpMethod.Get, "api/assets/" + Uri.EscapeDataString(id)), ct);
    public Task<AssetFormChoices> ChoicesAsync(bool editing, CancellationToken ct = default) => session.JsonAsync<AssetFormChoices>(() => new(HttpMethod.Get, "api/assets/form-choices?editing=" + editing.ToString()), ct);
    public Task<MediaFileView[]> PhotosAsync(string id, CancellationToken ct = default) => session.JsonAsync<MediaFileView[]>(() => new(HttpMethod.Get, "api/assets/" + Uri.EscapeDataString(id) + "/photos"), ct);
    public Task<MediaUploadResult> ReconcileAsync(string fileId, CancellationToken ct = default) => session.JsonAsync<MediaUploadResult>(() => new(HttpMethod.Post, "api/files/" + Uri.EscapeDataString(fileId) + "/reconcile"), ct);
    public Task<AssetWriteResult> WriteAsync(string? id, AssetWriteRequest request, string key, CancellationToken ct = default) => session.JsonAsync<AssetWriteResult>(() =>
    {
        var message = new HttpRequestMessage(id is null ? HttpMethod.Post : HttpMethod.Patch,
            "api/assets" + (id is null ? "" : "/" + Uri.EscapeDataString(id)))
        { Content = JsonContent.Create(request) };
        message.Headers.Add("Idempotency-Key", key); return message;
    }, ct);
    public Task<MediaUploadResult> UploadAsync(string id, PhotoSelection photo, long version, string description, bool primary, string key, CancellationToken ct = default) => session.JsonAsync<MediaUploadResult>(() =>
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(photo.Bytes); content.Headers.ContentType = new(photo.MimeType);
        form.Add(content, "file", photo.FileName);
        form.Add(new StringContent(version.ToString(CultureInfo.InvariantCulture)), "expectedVersion");
        form.Add(new StringContent(description), "description"); form.Add(new StringContent(primary.ToString()), "isPrimary");
        var message = new HttpRequestMessage(HttpMethod.Post, "api/assets/" + Uri.EscapeDataString(id) + "/photos") { Content = form };
        message.Headers.Add("Idempotency-Key", key); return message;
    }, ct);
}

// RAM only. Freeze the first sent DTO/key until a definitive result or explicit 409 rebase.
public sealed class MobileAssetDraft(MobileAssets api, string? assetId, AssetInput input, long version, string treeVersion)
{
    public string? AssetId { get; } = assetId;
    public AssetInput Input { get; set; } = input;
    public long Version { get; private set; } = version;
    public string TreeVersion { get; private set; } = treeVersion;
    public bool HasPending => pending is not null;
    public int? LastError { get; private set; }
    public AssetRecord? Saved { get; private set; }
    private AssetWriteRequest? pending;
    private string? key;
    private bool busy;
    public async Task<AssetWriteResult> SaveAsync(CancellationToken ct = default)
    {
        if (busy) throw new MobileApiException(409, "Đang lưu; chờ kết quả.");
        if (Saved is not null) return new(Saved, true);
        AssetInput normalized;
        try { normalized = AssetCrudService.ValidateInput(Input); }
        catch (AssetValidationException e) { throw new MobileApiException(422, e.Message); }
        var current = new AssetWriteRequest(normalized, Version, TreeVersion);
        if (pending is not null && JsonSerializer.Serialize(current, MobileJson.Options) != JsonSerializer.Serialize(pending, MobileJson.Options))
            throw new MobileApiException(409, "Yêu cầu cũ chưa xác nhận. Giữ nội dung đã gửi và retry cùng yêu cầu.");
        pending ??= current with { Asset = current.Asset with { Aliases = current.Asset.Aliases?.ToArray() } };
        key ??= Guid.NewGuid().ToString("N"); busy = true;
        try
        {
            var result = await api.WriteAsync(AssetId, pending, key, ct);
            if (result.Asset is null || string.IsNullOrEmpty(result.Asset.AssetId) || result.Asset.DataState != "DRAFT_DEMO" ||
                AssetId is not null && result.Asset.AssetId != AssetId)
                throw new MobileApiException(503, "Phản hồi lưu không đúng tài sản/DRAFT_DEMO; chưa xác nhận thành công.");
            Saved = result.Asset; pending = null; key = null; LastError = null; return result;
        }
        catch (MobileApiException e)
        { LastError = e.Status; if (e.Status is 400 or 422) { pending = null; key = null; } throw; }
        finally { busy = false; }
    }
    public async Task<AssetFormChoices> RebaseAsync(CancellationToken ct = default)
    {
        if (busy || LastError != 409) throw new MobileApiException(409, "Chỉ nạp version mới sau xung đột đã được backend từ chối.");
        busy = true;
        try
        {
            var choices = await api.ChoicesAsync(AssetId is not null, ct);
            var fresh = AssetId is null ? null : await api.DetailAsync(AssetId, ct);
            Version = fresh?.Asset.Version ?? 0; TreeVersion = choices.TreeVersionId ?? "";
            pending = null; key = null; LastError = null; // Input intentionally retained for user review.
            return choices;
        }
        finally { busy = false; }
    }
}
