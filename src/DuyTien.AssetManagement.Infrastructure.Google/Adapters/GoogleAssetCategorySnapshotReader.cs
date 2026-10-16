using System.Net.Http.Json;
using System.Text.Json;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Diagnostics;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
namespace DuyTien.AssetManagement.Infrastructure.Google.Adapters;

internal sealed record GoogleReadValueRange(string Range, JsonElement[][]? Values);
internal sealed record GooglePreparedRead<T>(string[] Ranges, Func<GoogleReadValueRange[], CancellationToken, Task<T>> Materialize);

// No cache or in-flight sharing across requests/users. Both parsers keep their complete validation.
public sealed class GoogleAssetCategorySnapshotReader(GoogleApiRequestExecutor api, GoogleAssetCatalogStore assets, GoogleCategoryCatalogStore categories) : IAssetCategorySnapshotReader
{
    public bool CanReadTogether(ICategoryCatalogStore other) => ReferenceEquals(categories, other);
    private sealed record BatchValues(GoogleReadValueRange[] ValueRanges);
    public async Task<(AssetCatalogState Assets, CategoryCatalogState Categories)> ReadAssetCategoriesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var source = assets.ReadSourceKey;
        void SameSource()
        {
            if (source != assets.ReadSourceKey || source != categories.ReadSourceKey || !ReferenceEquals(api, assets.ReadExecutor) || !ReferenceEquals(api, categories.ReadExecutor))
                throw new ExternalStorageException("Không được gom snapshot khác workspace/nguồn/executor.");
        }
        SameSource();
        using var metric = PerformanceTrace.Measure("business-batch");
        using var metadataScope = GoogleMetadataReadMemo.JoinOrBegin();
        var assetPlan = await assets.PrepareReadAsync(ct);
        var categoryPlan = await categories.PrepareReadAsync(ct);
        SameSource();
        var ranges = assetPlan.Ranges.Concat(categoryPlan.Ranges).Distinct(StringComparer.Ordinal).ToArray();
        var packets = new Dictionary<string, GoogleReadValueRange>(StringComparer.Ordinal);
        if (ranges.Length > 0)
        {
            var query = string.Join('&', ranges.Select(r => "ranges=" + Uri.EscapeDataString(r)));
            using var response = await api.SendAsync(() => new HttpRequestMessage(HttpMethod.Get, new Uri(source + "/values:batchGet?" + query + "&valueRenderOption=UNFORMATTED_VALUE")), GoogleRequestSemantics.SafeRead, ct);
            BatchValues batch;
            try { batch = await response.Content.ReadFromJsonAsync<BatchValues>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct) ?? throw new ExternalStorageException("Thiếu batch snapshot."); }
            catch (JsonException) { throw new ExternalStorageException("JSON batch snapshot không hợp lệ."); }
            catch (HttpRequestException e) { throw new ExternalStorageTransientException("Không đọc được batch snapshot.", false, e); }
            catch (IOException e) { throw new ExternalStorageTransientException("Không đọc được batch snapshot.", false, e); }
            catch (OperationCanceledException e) when (!ct.IsCancellationRequested) { throw new ExternalStorageTransientException("Batch snapshot timeout.", false, e); }
            if (batch.ValueRanges?.Length != ranges.Length) throw new ExternalStorageException("Batch snapshot thiếu range.");
            for (var i = 0; i < ranges.Length; i++)
            {
                var packet = batch.ValueRanges[i]; var title = ranges[i].Split('!')[0];
                if (packet?.Range is null || !packet.Range.StartsWith(title + "!", StringComparison.Ordinal) && !packet.Range.StartsWith(title.Trim('\'') + "!", StringComparison.Ordinal))
                    throw new ExternalStorageException("Batch snapshot trả range khác yêu cầu.");
                packets.Add(ranges[i], packet);
            }
        }
        ct.ThrowIfCancellationRequested(); SameSource();
        var assetState = await assetPlan.Materialize(assetPlan.Ranges.Select(r => packets[r]).ToArray(), ct);
        var categoryState = await categoryPlan.Materialize(categoryPlan.Ranges.Select(r => packets[r]).ToArray(), ct);
        return (assetState, categoryState);
    }
}
