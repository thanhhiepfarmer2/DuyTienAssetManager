using System.Text.Json;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Server.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DuyTien.AssetManagement.Server.Pages.Items;

[Authorize, RequestSizeLimit(64 * 1024 * 1024), RequestFormLimits(ValueLengthLimit = 48 * 1024 * 1024, MultipartBodyLengthLimit = 40 * 1024 * 1024)]
public sealed class ImportModel(InventoryService service) : InventoryPageModel
{
    [BindProperty] public IFormFile? Upload { get; set; }
    [BindProperty] public string? DocumentJson { get; set; }
    [BindProperty] public string? GroupsJson { get; set; } = "[]";
    [BindProperty] public string? DecisionsJson { get; set; } = "[]";
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public string? RequestKey { get; set; } = Guid.NewGuid().ToString("N");
    [BindProperty] public string? PreviewHash { get; set; }
    [BindProperty] public long Revision { get; set; }
    [BindProperty] public bool Confirm { get; set; }
    [BindProperty] public string? JobId { get; set; }
    [BindProperty] public int ExpectedProgress { get; set; }
    [BindProperty] public List<GroupForm> GroupRows { get; set; } = [];
    [BindProperty] public List<DuplicateForm> DuplicateRows { get; set; } = [];
    public sealed class GroupForm { public string? SourceGroup { get; set; } public string? CategoryId { get; set; } public string? Tracking { get; set; } }
    public sealed class DuplicateForm { public string? SourceCode { get; set; } public string? Mode { get; set; } public string? Reason { get; set; } public List<IdentityForm> Identities { get; set; } = []; }
    public sealed class IdentityForm { public int SourceRow { get; set; } public string? Identity { get; set; } }
    public ItemPreview? Preview { get; private set; }
    public ItemImportJob? Saved { get; private set; }
    public InventoryChoices Choices { get; private set; } = new([], []);
    public IReadOnlyList<ItemImportJob> Jobs { get; private set; } = [];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) { Response.Headers.CacheControl = "no-store"; try { await Load(ct); return Page(); } catch (Exception e) when (InventoryWeb.Expected(e)) { return Failure(e); } }
    private async Task Load(CancellationToken ct) { Choices = await service.ChoicesAsync(Actor, ct: ct); Jobs = await service.JobsAsync(Actor, ct); }
    private async Task<ItemImportRequest> Input(CancellationToken ct)
    {
        if (!ModelState.IsValid) throw new InventoryValidationException("Input form không hợp lệ.");
        try
        {
            var d = Upload is not null ? await InventoryWeb.ReadUpload(Upload, ct) : JsonSerializer.Deserialize<ItemDocument>(DocumentJson ?? "", Json) ?? throw new InventoryValidationException("JSON document null.");
            DocumentJson = JsonSerializer.Serialize(d, Json); ModelState.Remove(nameof(DocumentJson));
            var errors = ItemImportReader.Validate(d); if (errors.Count > 0) throw new InventoryValidationException(errors[0].Message);
            var groups = JsonSerializer.Deserialize<ItemGroupMapping[]>(GroupsJson ?? "[]", Json) ?? [];
            var decisions = JsonSerializer.Deserialize<ItemDuplicateDecision[]>(DecisionsJson ?? "[]", Json) ?? [];
            if (groups.Any(g => g is null || g.SourceGroup is null) || groups.GroupBy(g => g.SourceGroup, StringComparer.Ordinal).Any(g => g.Count() > 1) || decisions.Any(d => d is null || d.SourceCode is null) || decisions.GroupBy(d => d.SourceCode, StringComparer.Ordinal).Any(g => g.Count() > 1) || DuplicateRows.Any(d => d.Identities.GroupBy(i => i.SourceRow).Any(g => g.Count() > 1))) throw new InventoryValidationException("Mapping/quyết định có khóa trùng hoặc null.");
            if (GroupRows.Count > 0) groups = GroupRows.Select(g => new ItemGroupMapping(g.SourceGroup ?? "", g.CategoryId, g.Tracking switch { "YES" => true, "NO" => false, _ => (bool?)null })).ToArray();
            else GroupRows = d.Rows.Select(r => r.Cells[3]).Distinct(StringComparer.Ordinal).Select(name => { var g = groups.SingleOrDefault(g => g.SourceGroup == name); return new GroupForm { SourceGroup = name, CategoryId = g?.CategoryId, Tracking = g?.StockTracked switch { true => "YES", false => "NO", _ => "" } }; }).ToList();
            if (DuplicateRows.Count > 0) decisions = DuplicateRows.Where(d => !string.IsNullOrEmpty(d.Mode)).Select(d => new ItemDuplicateDecision(d.SourceCode ?? "", d.Mode ?? "", d.Mode == "SPLIT" ? d.Identities.GroupBy(i => i.SourceRow).ToDictionary(g => g.Key, g => g.First().Identity ?? "") : null, d.Reason ?? "")).ToArray();
            else DuplicateRows = d.Rows.GroupBy(r => r.Cells[0], StringComparer.Ordinal).Where(g => g.Count() > 1).Select(group => { var decision = decisions.SingleOrDefault(d => d.SourceCode == group.Key); return new DuplicateForm { SourceCode = group.Key, Mode = decision?.Mode, Reason = decision?.Reason, Identities = group.Select(r => new IdentityForm { SourceRow = r.SourceRow, Identity = decision?.Identities?.GetValueOrDefault(r.SourceRow) }).ToList() }; }).ToList();
            return new(d, groups, decisions, Reason ?? "");
        }
        catch (JsonException) { throw new InventoryValidationException("JSON mapping/decision không đúng cấu trúc."); }
    }
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; try { var r = await Input(ct); await Load(ct); Preview = await service.PreviewAsync(Actor, r, ct: ct); Revision = Preview.Revision; PreviewHash = Preview.PreviewHash; ModelState.Remove(nameof(Revision)); ModelState.Remove(nameof(PreviewHash)); return Page(); } catch (Exception e) when (InventoryWeb.Expected(e)) { return Failure(e); } }
    public async Task<IActionResult> OnPostStartAsync(CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; try { var r = await Input(ct); Saved = await service.StartAsync(Actor, r, Revision, PreviewHash ?? "", RequestKey ?? "", Confirm, ct); return Page(); } catch (Exception e) when (InventoryWeb.Expected(e)) { return Failure(e); } }
    public async Task<IActionResult> OnPostChunkAsync(CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; try { if (!ModelState.IsValid) throw new InventoryValidationException("Chunk form không hợp lệ."); Saved = await service.CommitChunkAsync(Actor, JobId ?? "", ExpectedProgress, RequestKey ?? "", ct); return Page(); } catch (Exception e) when (InventoryWeb.Expected(e)) { return Failure(e); } }
}
