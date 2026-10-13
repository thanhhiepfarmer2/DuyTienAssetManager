using System.Text.Json;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

public sealed class GoogleTreeMutationStore(GoogleWorkspaceOptions options, GoogleApiRequestExecutor api,
    GoogleCategoryCatalogStore categories, GoogleAssetCatalogStore assets,
    GoogleInventoryStore? inventory = null, GoogleMediaCatalogStore? media = null) : ITreeMutationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<TreeMutationSnapshot> ReadAsync(CancellationToken ct = default)
    {
        await GuardMetadata(ct);
        var snapshot = new TreeMutationSnapshot(await categories.ReadAsync(ct), await assets.ReadAsync(ct),
            inventory is null ? null : await inventory.ReadAsync(ct), media is null ? null : await media.ReadAsync(ct));
        TreeRegistryIntegrity.Validate(snapshot);
        foreach (var op in snapshot.Categories.TreeChanges.Where(o => o.Result.RegistryReconciled))
        {
            var checkpoint = op.RegistryCheckpoint ?? throw new ExternalStorageException("Tree receipt thiếu checkpoint registry.");
            if (checkpoint.InventoryRevision > (snapshot.Inventory?.Revision ?? 0) || checkpoint.MediaRevision > (snapshot.Media?.Revision ?? 0)) throw new ExternalStorageException("Registry mất revision/journal đã đối soát.");
            if (op.Result.ChangedItemCount > 0)
            {
                var itemOp = snapshot.Inventory?.Operations.ElementAtOrDefault(checked((int)checkpoint.InventoryRevision));
                if (itemOp?.OperationId != op.OperationId + ":ITEMS" || itemOp.RequestHash != op.RequestHash || itemOp.Items.Length != op.Result.ChangedItemCount) throw new ExternalStorageException("Tree/Item ledger không khớp.");
            }
            var documentOps = snapshot.Media?.Operations.Skip(checked((int)checkpoint.MediaRevision)).Take(op.Result.ChangedDocumentCount).ToArray() ?? [];
            if (documentOps.Length != op.Result.ChangedDocumentCount || documentOps.Any(o => !o.OperationId.StartsWith(op.OperationId + ":DOC:", StringComparison.Ordinal) || o.RequestHash != op.RequestHash)) throw new ExternalStorageException("Tree/Document ledger không khớp.");
        }
        return snapshot;
    }
    public async Task CommitAsync(TreeMutationSnapshot candidate, long expectedTreeRevision, long expectedAssetRevision, CancellationToken ct = default)
    {
        await GuardMetadata(ct); var previous = await ReadAsync(ct);
        var op = candidate.Categories.TreeChanges.LastOrDefault() ?? throw new CategoryConflictException("Thiếu journal cây.");
        var checkpoint = op.RegistryCheckpoint ?? new TreeRegistryCheckpoint(0, 0);
        if (previous.Categories.Revision != expectedTreeRevision || previous.Assets.Revision != expectedAssetRevision ||
            (previous.Inventory?.Revision ?? 0) != checkpoint.InventoryRevision || (previous.Media?.Revision ?? 0) != checkpoint.MediaRevision)
            throw new CategoryConflictException("Revision cây/tài sản/registry đã thay đổi; preview lại.");
        TreeRegistryIntegrity.Validate(candidate);
        var requests = await categories.PrepareAsync(candidate.Categories, expectedTreeRevision, ct);
        int[] Allocated() => requests.Select(r => JsonSerializer.SerializeToElement(r, Json)).Where(r => r.TryGetProperty("addSheet", out _))
            .Select(r => r.GetProperty("addSheet").GetProperty("properties").GetProperty("sheetId").GetInt32()).ToArray();
        try
        {
            if (candidate.Assets.Revision != expectedAssetRevision) requests.AddRange(await assets.PrepareAsync(candidate.Assets, expectedAssetRevision, ct, Allocated()));
            else Unchanged(candidate.Assets, previous.Assets);
            if ((candidate.Inventory?.Revision ?? 0) != checkpoint.InventoryRevision)
            {
                if (inventory is null || candidate.Inventory is null || candidate.Inventory.Revision != checkpoint.InventoryRevision + 1) throw new CategoryConflictException("Item candidate không hợp lệ.");
                requests.AddRange(await inventory.PrepareAsync(candidate.Inventory, checkpoint.InventoryRevision, ct, Allocated()));
            }
            else Unchanged(candidate.Inventory, previous.Inventory);
            if ((candidate.Media?.Revision ?? 0) != checkpoint.MediaRevision)
            {
                if (media is null || candidate.Media is null || candidate.Media.Revision != checkpoint.MediaRevision + op.Result.ChangedDocumentCount) throw new CategoryConflictException("Document candidate không hợp lệ.");
                requests.AddRange(await media.PrepareAsync(candidate.Media, checkpoint.MediaRevision, ct, Allocated(), treeBatch: true));
            }
            else Unchanged(candidate.Media, previous.Media);
        }
        catch (Exception e) when (e is InventoryConflictException or MediaConflictException) { throw new CategoryConflictException("Registry revision/ledger đổi; preview lại, không overwrite."); }
        catch (Exception e) when (e is InventoryValidationException or MediaValidationException) { throw new CategoryValidationException("Registry candidate/atomic plan vượt giới hạn hoặc chưa hợp lệ; chưa ghi."); }
        var body = JsonSerializer.SerializeToUtf8Bytes(new { requests }, Json);
        if (body.Length > 1800 * 1024) throw new CategoryValidationException("Atomic tree+registry batch vượt1800KiB; chưa ghi, không publish partial.");
        try
        {
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, UriFor(":batchUpdate"))
            { Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } } }, GoogleRequestSemantics.NonIdempotentWrite, ct);
        }
        catch (ExternalStorageException)
        {
            var actual = await ReadAsync(ct);
            if (actual.Categories.Revision == candidate.Categories.Revision && actual.Assets.Revision == candidate.Assets.Revision &&
                (actual.Inventory?.Revision ?? 0) == (candidate.Inventory?.Revision ?? 0) && (actual.Media?.Revision ?? 0) == (candidate.Media?.Revision ?? 0) &&
                actual.Categories.ActiveTreeVersionId == candidate.Categories.ActiveTreeVersionId &&
                JsonSerializer.Serialize(actual.Categories.TreeChanges.LastOrDefault(), Json) == JsonSerializer.Serialize(op, Json)) return;
            throw;
        }
    }
    private static void Unchanged(object? candidate, object? previous)
    { if (JsonSerializer.Serialize(candidate, Json) != JsonSerializer.Serialize(previous, Json)) throw new CategoryConflictException("Không đổi registry ngoài ledger atomic."); }
    private async Task GuardMetadata(CancellationToken ct)
    {
        if (options.GetMissingSheetsSettings().Count > 0 || string.IsNullOrWhiteSpace(options.SystemSpreadsheetId) || options.SystemSpreadsheetId == options.SpreadsheetId) throw new ExternalStorageException("Business spreadsheet phải hợp lệ và khác system.");
        using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, UriFor("?fields=sheets.properties.title")), GoogleRequestSemantics.SafeRead, ct);
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var titles = document.RootElement.GetProperty("sheets").EnumerateArray().Select(s => s.GetProperty("properties").GetProperty("title").GetString()).ToArray();
            var known = GoogleCategoryCatalogStore.Headers.Keys.Concat(["TreeChanges", "TreeChangeDetails", "TreeRegistryDetails"])
                .Concat(GoogleAssetCatalogStore.Headers.Keys).Concat(GoogleAssetCatalogStore.ReferenceHeaders.Keys).Concat(GoogleInventoryStore.Headers.Keys).Concat(GoogleMediaCatalogStore.Headers.Keys).ToHashSet(StringComparer.Ordinal);
            if (titles.Any(t => t is null || !known.Contains(t)) || titles.Distinct(StringComparer.Ordinal).Count() != titles.Length) throw new ExternalStorageException("Bảng metadata ngoài registry đã đối soát/không hợp lệ; không ghi cây.");
            if (inventory is null && titles.Any(t => GoogleInventoryStore.Headers.ContainsKey(t!)) || media is null && titles.Any(t => GoogleMediaCatalogStore.Headers.ContainsKey(t!)))
                throw new ExternalStorageException("Có bảng đối tượng chưa có adapter đối soát; không coi là rỗng và không ghi cây.");
            // Registered adapters MUST validate complete headers, journal and owner references before returning.
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { throw new ExternalStorageException("Metadata business không hợp lệ; không ghi cây."); }
        catch (HttpRequestException e) { throw new ExternalStorageTransientException("Không đọc được metadata business.", false, e); }
        catch (IOException e) { throw new ExternalStorageTransientException("Không đọc được metadata business.", false, e); }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested) { throw new ExternalStorageTransientException("Metadata business timeout.", false, e); }
    }
    private Uri UriFor(string suffix) => new(new Uri(options.SheetsApiBaseUrl), "spreadsheets/" + Uri.EscapeDataString(options.SpreadsheetId!) + suffix);
}
