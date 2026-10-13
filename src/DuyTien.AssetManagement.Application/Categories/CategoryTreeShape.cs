using System.Text.RegularExpressions;
namespace DuyTien.AssetManagement.Application.Categories;

public static class CategoryTreeShape
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<CategoryNode> nodes)
    {
        var issues = new List<string>();
        if (nodes.Count is < 1 or > 1000) issues.Add("Snapshot phải có1–1000 nút.");
        if (nodes.Select(n => n.CategoryId).Distinct(StringComparer.Ordinal).Count() != nodes.Count) issues.Add("Trùng CategoryId.");
        if (nodes.Select(n => n.SegmentCode).Distinct(StringComparer.Ordinal).Count() != nodes.Count) issues.Add("Trùng mã đoạn; không tự hợp nhất.");
        if (issues.Count > 0) return issues;
        var byId = nodes.ToDictionary(n => n.CategoryId, StringComparer.Ordinal);
        foreach (var n in nodes)
        {
            if (string.IsNullOrWhiteSpace(n.CategoryId) || string.IsNullOrWhiteSpace(n.Name) || n.Name.Length > 300 || n.Name.Any(char.IsControl) || !Regex.IsMatch(n.SegmentCode, "^[A-Z][A-Z0-9]{2}$", RegexOptions.CultureInvariant) || n.Definition.Length > 4000 || n.Definition.Any(ch => char.IsControl(ch) && ch is not ('\r' or '\n' or '\t')) || n.Version < 1 || n.State is not ("DRAFT" or "DRAFT_INACTIVE")) issues.Add("Node/name/code/definition/state không hợp lệ.");
            var chain = new List<CategoryNode> { n }; var seen = new HashSet<string>(StringComparer.Ordinal) { n.CategoryId }; var parent = n.ParentId;
            while (parent is not null)
            {
                if (!byId.TryGetValue(parent, out var p)) { issues.Add("Thiếu cha."); break; }
                if (!seen.Add(parent)) { issues.Add("Cycle không được phép."); break; }
                chain.Insert(0, p); parent = p.ParentId;
            }
            if (chain.Count > 10) issues.Add("F11 bị chặn; tối đa F10.");
            if (parent is null && (n.FLevel != chain.Count || n.FullCode != string.Join('.', chain.Select(p => p.SegmentCode)))) issues.Add("Level/full path không khớp cây.");
            if (n.State == "DRAFT" && chain.Any(p => p.State != "DRAFT")) issues.Add("Node đang dùng không thể có cha ngừng dùng.");
        }
        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }
}
