using System.Net.Http;
using DuyTien.AssetManagement.Application.Assets;
using DuyTien.AssetManagement.Application.ExternalStorage;
using DuyTien.AssetManagement.Infrastructure.Google.Adapters;
using DuyTien.AssetManagement.Infrastructure.Google.Configuration;
using DuyTien.AssetManagement.Infrastructure.Google.Http;
using DuyTien.AssetManagement.Infrastructure.Google.OAuth;
namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class GoogleAssetCatalogTests
{
    [Fact]
    public async Task Atomic_batch_persists_counter_asset_alias_journal_audit_and_reads_after_reconstruction()
    {
        var h = Handler(); var f = await AssetFixture.Start(Store(h)); var a = (await f.Create(f.Input() with { Notes = "=FORMULA_IS_TEXT", Aliases = ["000-OLD"] })).Asset;
        Assert.Equal(1, h.Posts); Assert.Equal(9, h.Tables.Count); Assert.Equal(2, h.Tables["Assets"].Rows.Count); Assert.Equal(2, h.Tables["AssetAuditEvents"].Rows.Count); Assert.DoesNotContain("formulaValue", h.LastBody, StringComparison.Ordinal);
        var persisted = await Store(h).ReadAsync(); Assert.Equal(a, persisted.Assets.Single()); Assert.Equal(1, persisted.Counters.Single().LastAllocated); Assert.Equal("000-OLD", persisted.Aliases.Single().OldCode);
        var restarted = f.NewService(Store(h)); var repeat = await restarted.CreateAsync("ADMIN", f.Request(f.Input() with { Notes = "=FORMULA_IS_TEXT", Aliases = ["000-OLD"] }), persisted.Operations.Single().IdempotencyKey);
        Assert.True(repeat.Replayed); Assert.Equal(1, h.Posts);
        await restarted.UpdateAsync("ADMIN", a.AssetId, f.Request(f.Input() with { Name = "Sửa giả", LocationId = "LOC-A-CHILD", Aliases = ["000-OLD"] }, 1), "update");
        Assert.Equal(2, h.Posts); Assert.Equal(2, h.Tables["Assets"].Rows.Count); Assert.Equal(3, h.Tables["AssetAuditEvents"].Rows.Count); Assert.Equal(4, h.Tables["OrgUnits"].Rows.Count);
        Assert.All(h.Uris, u => Assert.Contains("business-fake", u, StringComparison.Ordinal)); Assert.DoesNotContain(h.Uris, u => u.Contains("system-fake", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Lost_response_is_reconciled_and_soft_delete_does_not_reallocate_code()
    {
        var h = Handler(); h.LoseResponseOnce = true; var f = await AssetFixture.Start(Store(h)); var a = (await f.Create()).Asset;
        Assert.Equal(1, h.Posts); await f.Service.DeleteAsync("ADMIN", a.AssetId, new(1, "fixture"), "delete");
        var second = (await f.Create()).Asset; Assert.Equal("TEST-TST-000002", second.ObjectCode);
        var r = await f.Service.RestoreAsync("ADMIN", a.AssetId, new(2, "fixture"), "restore"); Assert.Equal(a.ObjectCode, r.Asset.ObjectCode); Assert.Equal(4, h.Posts);
    }
    [Fact]
    public async Task Failure_before_commit_keeps_counter_empty_and_explicit_retry_creates_once()
    {
        var h = Handler(); h.FailBeforeCommitOnce = true; var f = await AssetFixture.Start(Store(h)); var request = f.Request();
        await Assert.ThrowsAsync<ExternalStorageTransientException>(() => f.Service.CreateAsync("ADMIN", request, "same-key"));
        Assert.Empty((await Store(h).ReadAsync()).Assets);
        var result = await f.Service.CreateAsync("ADMIN", request, "same-key"); Assert.False(result.Replayed); Assert.Equal("TEST-TST-000001", result.Asset.ObjectCode); Assert.Single((await Store(h).ReadAsync()).Assets);
    }
    [Fact]
    public async Task Unknown_outcome_and_failed_reconciliation_recovers_via_same_key_after_restart()
    {
        var h = Handler(); h.LoseResponseOnce = true; var failure = new FailOneReadAfterWrite(h); var f = await AssetFixture.Start(Store(h, failure));
        await Assert.ThrowsAsync<ExternalStorageTransientException>(() => f.Service.CreateAsync("ADMIN", f.Request(), "lost"));
        var restarted = f.NewService(Store(h)); var result = await restarted.CreateAsync("ADMIN", f.Request(), "lost");
        Assert.True(result.Replayed); Assert.Equal(1, h.Posts); Assert.Single((await Store(h).ReadAsync()).Assets);
        await Assert.ThrowsAsync<AssetConflictException>(() => restarted.CreateAsync("ADMIN", f.Request(f.Input() with { Name = "Changed" }), "lost"));
    }
    [Theory]
    [InlineData("partial")]
    [InlineData("header")]
    [InlineData("counter")]
    [InlineData("asset")]
    [InlineData("audit")]
    [InlineData("alias")]
    [InlineData("range")]
    [InlineData("reference-cycle")]
    public async Task Unknown_schema_or_unmanaged_changes_fail_closed_without_overwrite(string mode)
    {
        var h = Handler(); var store = Store(h); var f = await AssetFixture.Start(store); await f.Create(f.Input() with { Aliases = ["legacy"] }); var writes = h.Posts;
        if (mode == "partial") h.Tables.Remove("AssetAuditEvents");
        else if (mode == "header") h.Tables["Assets"].Rows[0][0] = "unknown";
        else if (mode == "counter") h.Tables["IdCounters"].Rows[1][1] = "0";
        else if (mode == "asset") h.Tables["Assets"].Rows[1][5] = "Unmanaged overwrite";
        else if (mode == "audit") h.Tables["AssetAuditEvents"].Rows[1][4] = "UNKNOWN";
        else if (mode == "alias") h.Tables["LegacyAliases"].Rows[1][2] = "Changed alias";
        else if (mode == "range") h.WrongRange = true;
        else h.Tables["OrgUnits"].Rows[1][1] = "OU-A";
        await Assert.ThrowsAsync<ExternalStorageException>(() => store.ReadAsync()); Assert.Equal(writes, h.Posts);
    }
    [Fact]
    public async Task No_reference_or_rule_is_seeded_and_equal_system_business_id_is_rejected_before_network()
    {
        var h = new GoogleCategoryCatalogStoreTests.CategorySheetsHandler(); var f = await AssetFixture.Start(Store(h));
        await Assert.ThrowsAsync<AssetValidationException>(() => f.Create()); Assert.Equal(0, h.Posts); Assert.Empty(h.Tables);
        var options = withSystem();
        GoogleWorkspaceOptions withSystem() => new() { SpreadsheetId = "business-fake", SystemSpreadsheetId = "business-fake", OAuthClientId = "fake-client", OAuthClientSecret = "fake-config", OAuthRefreshToken = "fake-config" };
        var store = new GoogleAssetCatalogStore(options, new(options, new Token(), new HttpClient(h)));
        h.Uris.Clear(); await Assert.ThrowsAsync<ExternalStorageException>(() => store.ReadAsync()); Assert.Empty(h.Uris);
    }
    [Fact]
    public async Task Stale_revision_and_history_rewrite_are_rejected_before_post()
    {
        var h = Handler(); var store = Store(h); var f = await AssetFixture.Start(store); await f.Create(); var state = await store.ReadAsync();
        await Assert.ThrowsAsync<AssetConflictException>(() => store.CommitAsync(state, 0));
        var next = state.Clone(); next.Revision++; next.Operations.Add(state.Operations[0]);
        await Assert.ThrowsAsync<ExternalStorageException>(() => store.CommitAsync(next, state.Revision)); Assert.Equal(1, h.Posts);
    }
    [Fact]
    public async Task Physical_asset_row_sort_is_resolved_by_identity_without_resetting_counter()
    {
        var h = Handler(); var store = Store(h); var f = await AssetFixture.Start(store); var first = (await f.Create()).Asset; await f.Create();
        h.Tables["Assets"].Rows.Reverse(1, 2); var loaded = await store.ReadAsync(); Assert.Equal(2, loaded.Assets.Count);
        await f.Service.UpdateAsync("ADMIN", first.AssetId, f.Request(f.Input() with { Name = "After sort" }, 1), "sort-update");
        Assert.Equal(first.ObjectCode, (await store.ReadAsync()).Assets.Single(a => a.AssetId == first.AssetId).ObjectCode);
        Assert.Equal(2, (await store.ReadAsync()).Counters.Single().LastAllocated);
    }
    [Theory]
    [InlineData("metadata-null")]
    [InlineData("metadata-json")]
    [InlineData("values-null")]
    [InlineData("null-row")]
    public async Task Malformed_upstream_json_and_null_rows_are_safe_storage_errors_without_post(string fault)
    {
        var h = Handler(); var pipeline = new MalformedResponse(h, fault); var store = Store(h, pipeline);
        await Assert.ThrowsAsync<ExternalStorageException>(() => store.ReadAsync()); Assert.Equal(0, h.Posts);
    }
    private sealed class MalformedResponse(GoogleCategoryCatalogStoreTests.CategorySheetsHandler h, string fault) : DelegatingHandler(h)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var values = request.RequestUri!.AbsolutePath.Contains("values:batchGet", StringComparison.Ordinal);
            if (!values && fault.StartsWith("metadata", StringComparison.Ordinal) || values && !fault.StartsWith("metadata", StringComparison.Ordinal))
            {
                var body = fault switch { "metadata-null" => "{}", "metadata-json" => "not-json", "values-null" => "{}", _ => "{\"valueRanges\":[{\"range\":\"'CodeRules'!A1\",\"values\":[null]},{\"range\":\"'OrgUnits'!A1\"},{\"range\":\"'Locations'!A1\"}]}" };
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
            }
            return base.SendAsync(request, ct);
        }
    }
    [Fact]
    public async Task Oversized_alias_history_cell_is_rejected_before_atomic_write_without_discarding_mapping()
    {
        var h = Handler(); var store = Store(h); var f = await AssetFixture.Start(store);
        string[] Aliases(int batch) => Enumerable.Range(0, 20).Select(i => batch.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + new string('ẫ', 113)).ToArray();
        var asset = (await f.Create(f.Input() with { Aliases = Aliases(0) })).Asset;
        var rejected = false;
        for (var i = 1; i < 10; i++)
        {
            var posts = h.Posts; var before = await store.ReadAsync();
            try { asset = (await f.Service.UpdateAsync("ADMIN", asset.AssetId, f.Request(f.Input() with { Aliases = Aliases(i) }, asset.Version), "aliases-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))).Asset; }
            catch (AssetValidationException error)
            {
                Assert.Contains("45000", error.Message, StringComparison.Ordinal); Assert.Equal(posts, h.Posts);
                var after = await store.ReadAsync(); Assert.Equal(before.Revision, after.Revision); Assert.Equal(before.Aliases.Count, after.Aliases.Count); Assert.Equal(1, after.Counters.Single().LastAllocated); rejected = true; break;
            }
        }
        Assert.True(rejected, "Large synthetic alias history must be bounded before write");
    }
    private static GoogleWorkspaceOptions Options() => new() { SpreadsheetId = "business-fake", SystemSpreadsheetId = "system-fake", OAuthClientId = "fake-client", OAuthClientSecret = "fake-config", OAuthRefreshToken = "fake-config", MaxRetryAttempts = 1 };
    private static GoogleAssetCatalogStore Store(GoogleCategoryCatalogStoreTests.CategorySheetsHandler h, HttpMessageHandler? pipeline = null)
    { var o = Options(); return new(o, new(o, new Token(), new HttpClient(pipeline ?? h))); }
    private sealed class Token : IGoogleAccessTokenProvider { public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fake-adapter-token"); public void Invalidate() { } }
    private sealed class FailOneReadAfterWrite(GoogleCategoryCatalogStoreTests.CategorySheetsHandler handler) : DelegatingHandler(handler)
    {
        private bool pending = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { if (pending && request.Method == HttpMethod.Get && handler.Posts > 0) { pending = false; throw new HttpRequestException("Synthetic failed reconciliation"); } return base.SendAsync(request, ct); }
    }
    private static GoogleCategoryCatalogStoreTests.CategorySheetsHandler Handler()
    {
        var h = new GoogleCategoryCatalogStoreTests.CategorySheetsHandler(); var s = AssetFixture.References(); var id = 3000;
        foreach (var pair in GoogleAssetCatalogStore.ReferenceHeaders) { var table = new GoogleCategoryCatalogStoreTests.Table(id++, pair.Value.Length); table.Rows.Add(pair.Value.ToArray()); h.Tables[pair.Key] = table; }
        h.Tables["CodeRules"].Rows.AddRange(s.Rules.Select(r => new[] { r.RuleId, r.CategoryRootId, r.Prefix, r.Ccc, r.Status, r.ApprovedForDemo.ToString(), r.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        h.Tables["OrgUnits"].Rows.AddRange(s.OrgUnits.Select(o => new[] { o.Id, o.ParentId ?? "", o.Name, "ACTIVE", "1" }));
        h.Tables["Locations"].Rows.AddRange(s.Locations.Select(l => new[] { l.Id, l.ParentId ?? "", l.OrgUnitId, l.Name, "ACTIVE", "1" })); return h;
    }
}
