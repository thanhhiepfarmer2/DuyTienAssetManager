using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleTreeMutationStore(GoogleWorkspaceOptions options, GoogleApiRequestExecutor api,
    GoogleCategoryCatalogStore categories, GoogleAssetCatalogStore assets) : ITreeMutationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<TreeMutationSnapshot> ReadAsync(CancellationToken ct = default)
    {
        await GuardUnsupportedObjects(ct);
        return new(await categories.ReadAsync(ct), await assets.ReadAsync(ct));
    }
    public async Task CommitAsync(TreeMutationSnapshot candidate, long expectedTreeRevision, long expectedAssetRevision, CancellationToken ct = default)
    {
        await GuardUnsupportedObjects(ct);
        var previous = await ReadAsync(ct);
        if (previous.Categories.Revision != expectedTreeRevision || previous.Assets.Revision != expectedAssetRevision) throw new CategoryConflictException("Revision đã thay đổi; preview lại.");
        var requests = await categories.PrepareAsync(candidate.Categories, expectedTreeRevision, ct);
        if (candidate.Assets.Revision != expectedAssetRevision)
        {
            var allocated = requests.Select(r => JsonSerializer.SerializeToElement(r, Json)).Where(r => r.TryGetProperty("addSheet", out _)).Select(r => r.GetProperty("addSheet").GetProperty("properties").GetProperty("sheetId").GetInt32()).ToArray();
            requests.AddRange(await assets.PrepareAsync(candidate.Assets, expectedAssetRevision, ct, allocated));
        }
        else if (JsonSerializer.Serialize(candidate.Assets, Json) != JsonSerializer.Serialize(previous.Assets, Json)) throw new CategoryConflictException("Không được đổi tài sản ngoài ledger atomic.");
        var body = JsonSerializer.SerializeToUtf8Bytes(new { requests }, Json);
        if (body.Length > 1800 * 1024) throw new CategoryValidationException("Atomic tree+asset batch vượt1800KiB; chưa ghi, không publish partial.");
        try
        {
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, UriFor(":batchUpdate")) { Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } } }, GoogleRequestSemantics.NonIdempotentWrite, ct);
        }
        catch (ExternalStorageException)
        {
            var actual = await ReadAsync(ct);
            if (actual.Categories.Revision == candidate.Categories.Revision && actual.Assets.Revision == candidate.Assets.Revision && actual.Categories.ActiveTreeVersionId == candidate.Categories.ActiveTreeVersionId && JsonSerializer.Serialize(actual.Categories.TreeChanges.LastOrDefault(), Json) == JsonSerializer.Serialize(candidate.Categories.TreeChanges.Last(), Json)) return;
            throw;
        }
    }
    private async Task GuardUnsupportedObjects(CancellationToken ct)
    {
        if (options.GetMissingSheetsSettings().Count > 0 || string.IsNullOrWhiteSpace(options.SystemSpreadsheetId) || options.SystemSpreadsheetId == options.SpreadsheetId) throw new ExternalStorageException("Business spreadsheet phải hợp lệ và khác system.");
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("?fields=sheets.properties.title")), GoogleRequestSemantics.SafeRead, ct);
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var sheets = document.RootElement.GetProperty("sheets").EnumerateArray();
            if (sheets.Any(s => s.GetProperty("properties").GetProperty("title").GetString() is "Items" or "Documents" or "DocumentVersions" or "StockBalances" or "Files")) throw new ExternalStorageException("Có bảng đối tượng ngoài lát Asset TASK-008; chưa hỗ trợ đối soát, không coi là rỗng và không ghi cây.");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { throw new ExternalStorageException("Metadata business không hợp lệ; không ghi cây."); }
        catch (HttpRequestException e) { throw new ExternalStorageTransientException("Không đọc được metadata business.", false, e); }
        catch (IOException e) { throw new ExternalStorageTransientException("Không đọc được metadata business.", false, e); }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested) { throw new ExternalStorageTransientException("Metadata business timeout.", false, e); }
    }
    private Uri UriFor(string suffix) => new(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + suffix);
}
