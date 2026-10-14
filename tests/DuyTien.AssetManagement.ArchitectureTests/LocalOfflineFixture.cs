using System.Security.Cryptography;
using System.Text;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.Authentication;
using DuyTien.AssetManagement.Application.Categories;
using DuyTien.AssetManagement.Application.Inventory;
using DuyTien.AssetManagement.Application.Media;
using DuyTien.AssetManagement.Application.Reports;
using DuyTien.AssetManagement.Infrastructure.Local;
using DuyTien.AssetManagement.Server.Authentication;
namespace DuyTien.AssetManagement.ArchitectureTests;
// Entirely synthetic; actual production filesystem adapters, not an in-memory/fake store.
internal sealed class LocalOfflineFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "DuyTienOfflineTest-" + Guid.NewGuid().ToString("N"));
    public LocalWorkspace W { get; private set; } = null!;
    public string Admin { get; private set; } = ""; public string[] Roots { get; private set; } = []; public string Child { get; private set; } = "";
    public AuthApplicationService Auth => new(W, new FrameworkPasswordHashService(), new(), TimeProvider.System);
    public AssetCrudService Assets => new(W, W, W, W, TimeProvider.System);
    public CategoryCatalogService Categories => new(W, W, W, TimeProvider.System, W, W, W);
    public InventoryService Inventory => new(W, W, W, W, W, TimeProvider.System);
    public MediaLibraryService Media => new(W, W, W, W, W, W, TimeProvider.System, new());
    public ReportService Reports => new(W, W, W, W, W, TimeProvider.System);
    public TreeMutationService Tree => new(W, W, W, TimeProvider.System);
    public static async Task<LocalOfflineFixture> Start(int items = 3, bool largeTree = false)
    {
        var f = new LocalOfflineFixture(); Directory.CreateDirectory(f.Root); f.W = new(f.Root);
        try
        {
            f.Admin = (await f.Auth.BootstrapAdminAsync("aaaaaaaa", "FAKE fixture")).UserId;
            Assert.True(await f.Auth.ChangePasswordAsync(f.Admin, "aaaaaaaa", "bbbbbbbb"));
            var rows = Rows(largeTree); var doc = new CategoryDocument("FAKE synthetic fixture", CategoryImportReader.MappingVersion, rows, ["GIẢ; không phải nguồn công ty"]);
            var p = await f.Categories.PreviewAsync(f.Admin, doc); Assert.True(p.CanCommit, string.Join(" ", p.Issues.Take(5))); await f.Categories.ImportAsync(f.Admin, doc, p.Revision, p.PreviewHash, "fixture-tree", true);
            var tree = await ((ICategoryCatalogStore)f.W).ReadAsync(); f.Roots = tree.ActiveNodes.Where(n => n.ParentId is null).OrderBy(n => n.DisplayOrder).Select(n => n.CategoryId).ToArray(); f.Child = tree.ActiveNodes.First(n => n.FLevel == 2).CategoryId;
            await f.W.InitializeDemoReferencesAsync(f.Roots);
            if (largeTree) for (var n = 0; n < 5; n++) await f.Assets.CreateAsync(f.Admin, await f.Request("GIẢ benchmark máy " + n), "bench-asset-" + n);
            if (items > 0)
            {
                var state = BuildInventory(items, tree.ActiveTreeVersionId!, largeTree ? tree.ActiveNodes.Where(n => n.FLevel == 3).Select(n => n.CategoryId).ToArray() : [f.Child], f.Admin);
                await f.W.ExecuteAsync(async ct => { await ((IInventoryStore)f.W).CommitAsync(state, 0, ct); return true; });
            }
            return f;
        }
        catch { f.Dispose(); throw; }
    }
    public async Task<AssetWriteRequest> Request(string name = "GIẢ máy") => new(new(name, Child, "OU-FAKE", "LOC-FAKE"), 0, (await ((ICategoryCatalogStore)W).ReadAsync()).ActiveTreeVersionId!);
    public void Restart() { W.Dispose(); W = new(Root); }
    public void Dispose() { W?.Dispose(); var full = Path.GetFullPath(Root); if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()) + "DuyTienOfflineTest-", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected fixture cleanup target"); if (Directory.Exists(full)) Directory.Delete(full, true); }
    private static CategoryInput[] Rows(bool large)
    {
        var rows = new List<CategoryInput>(); int row = 2;
        string Code(char prefix, int n) => prefix + "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[n / 36].ToString() + "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[n % 36];
        for (var i = 0; i < (large ? 15 : 2); i++) rows.Add(new("F1-F3 Mã ngang", row++, (i + 1).ToString(), null, 1, "GIẢ nhóm " + (i + 1), Code('A', i), "FAKE_ONLY"));
        for (var i = 0; i < (large ? 98 : 1); i++) rows.Add(new("F1-F3 Mã ngang", row++, ((i % (large ? 15 : 1) + 1).ToString() + "." + (i / (large ? 15 : 1) + 1)), (i % (large ? 15 : 1) + 1).ToString(), 2, "GIẢ loại " + i, Code('B', i), "FAKE_ONLY"));
        for (var i = 0; i < (large ? 552 : 0); i++) rows.Add(new("F1-F3 Mã ngang", row++, ((i % 98 % 15 + 1).ToString() + "." + (i % 98 / 15 + 1) + "." + (i / 98 + 1)), ((i % 98 % 15 + 1).ToString() + "." + (i % 98 / 15 + 1)), 3, "GIẢ chi tiết " + i, Code('C', i), "FAKE_ONLY"));
        return rows.ToArray();
    }
    private static InventoryState BuildInventory(int count, string tree, string[] categories, string user)
    {
        // Categories are generated logical IDs; the principal and all data are explicitly fake.
        var s = new InventoryState(); var now = DateTimeOffset.UtcNow; var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("FAKE fixture " + count))); var job = new ItemImportJob("IMPORT-" + Guid.NewGuid().ToString("N"), user, hash, tree, "GIẢ benchmark; không công ty", "{\"SourceVerified\":false,\"Fixture\":true}", count, count, 0, "STAGING", now, now);
        void Apply(string action, ItemImportJob j, ItemImportPlanRow[] plan, ItemRecord[] items) { InventoryIntegrity.Apply(s, new("INV-" + Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), user, hash, action, "GIẢ fixture", now, j, plan, items, null)); s.Revision++; }
        Apply("IMPORT_START", job, [], []); var all = new List<ItemImportPlanRow>();
        for (var i = 0; i < count; i++) { var cells = Enumerable.Repeat("", 20).ToArray(); cells[0] = "FAKE-" + i.ToString("D6"); cells[1] = "GIẢ vật tư " + i; cells[2] = "Vật tư hàng hóa"; cells[3] = "GIẢ"; cells[5] = "cái"; var item = new ItemRecord("ITEM-" + Guid.NewGuid().ToString("N"), "MISA_VTHH", "CODE:" + cells[0], categories[i % categories.Length], true, cells, 0); all.Add(new(job.JobId, new(i + 3, item, 0, "ADD"))); }
        for (var start = 0; start < count; start += 100) { var plan = all.Skip(start).Take(100).ToArray(); job = job with { State = start + plan.Length == count ? "VALIDATED" : "STAGING" }; Apply("IMPORT_STAGE", job, plan, []); }
        while (job.Processed < count) { var plan = all.Skip(job.Processed).Take(InventoryService.NextChunkCount(all.Skip(job.Processed).Select(p => p.Row))).ToArray(); job = job with { Processed = job.Processed + plan.Length, State = job.Processed + plan.Length == count ? "COMPLETE" : "PARTIAL" }; Apply("IMPORT_CHUNK", job, [], plan.Select(p => p.Row.Item with { Version = 1 }).ToArray()); }
        InventoryIntegrity.Validate(s); return s;
    }
}
