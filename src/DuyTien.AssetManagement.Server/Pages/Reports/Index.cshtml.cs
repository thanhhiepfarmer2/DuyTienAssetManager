using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.Server.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
namespace DuyTien.AssetManagement.Server.Pages.Reports;

[Authorize]
public sealed class IndexModel(ReportService service) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Code { get; set; } = "R01";
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public bool IncludeDescendants { get; set; } = true;
    [BindProperty(SupportsGet = true)] public string? OrgUnitId { get; set; }
    [BindProperty(SupportsGet = true)] public string? LocationId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type { get; set; }
    [BindProperty(SupportsGet = true)] public string? Condition { get; set; }
    [BindProperty(SupportsGet = true)] public int? GroupLevel { get; set; }
    [BindProperty(SupportsGet = true)] public string? Unit { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? AsOfFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? AsOfTo { get; set; }
    [BindProperty(SupportsGet = true)] public string? Section { get; set; } = "MATCHES";
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    public bool IsStock => Code is "R03" or "R04";
    public ReportResult? Report { get; private set; }
    public string? Error { get; private set; }
    public ReportChoices Choices => Report?.Choices ?? new([], [], [], [], [], []);
    private ReportQuery Query() => new(Q, CategoryId, IncludeDescendants, OrgUnitId, LocationId, Type, Condition, GroupLevel, Unit, AsOfFrom, AsOfTo, Section ?? "MATCHES", PageNumber, PageSize);
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; if (!ModelState.IsValid) { Error = "Bộ lọc/số trang/thời điểm không hợp lệ."; Response.StatusCode = 400; return Page(); } try { Report = await service.ReadAsync(ReportWeb.Actor(User), Code ?? "R01", Query(), ct); return Page(); } catch (Exception e) when (ReportWeb.Expected(e)) { return Failure(e); } }
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    { Response.Headers.CacheControl = "no-store"; Response.Headers.XContentTypeOptions = "nosniff"; if (!ModelState.IsValid) { Error = "Bộ lọc export không hợp lệ."; Response.StatusCode = 400; return Page(); } try { var result = await service.ExportAsync(ReportWeb.Actor(User), Code ?? "R01", Query(), ct); return File(result.Bytes, result.ContentType, result.FileName); } catch (Exception e) when (ReportWeb.Expected(e)) { return Failure(e); } }
    private IActionResult Failure(Exception e) { if (e is ReportForbiddenException) return Forbid(); if (e is ReportNotFoundException) return NotFound(); Error = ReportWeb.Message(e); Response.StatusCode = ReportWeb.Status(e); return Page(); }
    public RouteValueDictionary Routes(int? page = null) => new() { ["Code"] = Code ?? "R01", ["Q"] = Q, ["CategoryId"] = CategoryId, ["IncludeDescendants"] = IncludeDescendants, ["OrgUnitId"] = OrgUnitId, ["LocationId"] = LocationId, ["Type"] = Type, ["Condition"] = Condition, ["GroupLevel"] = GroupLevel, ["Unit"] = Unit, ["AsOfFrom"] = AsOfFrom?.ToString("O"), ["AsOfTo"] = AsOfTo?.ToString("O"), ["Section"] = Section ?? "MATCHES", ["PageNumber"] = page ?? PageNumber, ["PageSize"] = PageSize };
}
