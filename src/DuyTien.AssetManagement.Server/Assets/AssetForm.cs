using System.ComponentModel.DataAnnotations;
using DuyTien.AssetManagement.Application.Assets;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace DuyTien.AssetManagement.Server.Assets;
public sealed class AssetForm
{
    [Required(ErrorMessage = "Nhập tên máy demo."), Display(Name = "Tên máy demo"), StringLength(300, ErrorMessage = "Tên tối đa 300 ký tự.")] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Chọn danh mục nháp."), Display(Name = "Danh mục DRAFT")] public string CategoryId { get; set; } = "";
    [Required(ErrorMessage = "Chọn đơn vị quản lý."), Display(Name = "Đơn vị quản lý")] public string OrgUnitId { get; set; } = "";
    [Required(ErrorMessage = "Chọn địa điểm."), Display(Name = "Địa điểm")] public string LocationId { get; set; } = "";
    [Display(Name = "Người quản lý (tham chiếu)")] public string? CustodianRef { get; set; } = "";
    [Display(Name = "Serial")] public string? Serial { get; set; } = "";
    [Display(Name = "Model")] public string? Model { get; set; } = "";
    [Display(Name = "Hãng")] public string? Manufacturer { get; set; } = "";
    [Display(Name = "Ngày ghi nhận"), DataType(DataType.Date)] public DateOnly? RecordedOn { get; set; }
    [Required(ErrorMessage = "Nhập tình trạng hoặc UNKNOWN."), Display(Name = "Tình trạng")] public string Condition { get; set; } = "UNKNOWN";
    [Display(Name = "Ghi chú")] public string? Notes { get; set; } = "";
    [Display(Name = "Alias demo (mỗi dòng một mã)")] public string? AliasesText { get; set; } = "";
    [BindNever] public AssetFormChoices Choices { get; set; } = new(null, [], [], []);
    public AssetInput Input() => new(Name, CategoryId, OrgUnitId, LocationId, "MACHINE", CustodianRef ?? "", Serial ?? "", Model ?? "", Manufacturer ?? "", RecordedOn, Condition, Notes ?? "",
        (AliasesText ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    public static AssetForm From(AssetView view) => new() { Name = view.Asset.Name, CategoryId = view.Asset.CategoryId, OrgUnitId = view.Asset.OrgUnitId, LocationId = view.Asset.LocationId, CustodianRef = view.Asset.CustodianRef, Serial = view.Asset.Serial, Model = view.Asset.Model, Manufacturer = view.Asset.Manufacturer, RecordedOn = view.Asset.RecordedOn, Condition = view.Asset.Condition, Notes = view.Asset.Notes, AliasesText = string.Join("\n", view.Aliases.Where(a => a.IsActive).Select(a => a.OldCode)) };
}
