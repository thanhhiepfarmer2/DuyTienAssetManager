using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace DuyTien.AssetManagement.Server.ConnectedCache;

public sealed record CentralProof(string UserId, string SessionId, string Username, string DisplayName, string[] Roles,
    long SecurityVersion, string AuthorityHash, DateTimeOffset VerifiedAt, DateTimeOffset ExpiresAt);
public sealed record CachedView(string Key, string File, string ContentType, string? Disposition, DateTimeOffset AsOf, int Size, Dictionary<string, string>? Dependencies = null, string? PresentationVersion = null);
public sealed record PendingWrite(string Method, string Path, string Key, string BodyHash, string State = "UNKNOWN", bool JournalConfirmed = false);
public sealed record CachedTicket(CentralProof Proof, string ReadyEpoch, bool Revoked, CachedView[] Views, PendingWrite? Pending, string SnapshotEpoch = "", Dictionary<string, string>? Checkpoints = null, string CredentialId = "");
public sealed class ProtectedQueryCache : IDisposable
{
    private readonly string root;
    private readonly IDataProtector protector;
    private readonly FileStream lease;
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly ConcurrentDictionary<string, CachedTicket> tickets = new();
    private readonly MemoryCache memory = new(new MemoryCacheOptions { SizeLimit = 128 * 1024 * 1024 });
    private sealed record UserPending(PendingWrite? Value);
    private readonly ConcurrentDictionary<string, UserPending> userPending = new();
    private const int MaxBytes = 64 * 1024 * 1024;
    public string CentralId { get; }
    public ProtectedQueryCache(string path, string centralId, IDataProtectionProvider protection)
    {
        PrepareRoot(path); CentralId = Hash(centralId); root = Path.Combine(path, CentralId); Directory.CreateDirectory(root);
        protector = protection.CreateProtector("DuyTien.ConnectedCache.v2", CentralId);
        lease = new FileStream(Path.Combine(root, "cache.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public static void PrepareRoot(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\")) throw new InvalidOperationException("Cache cần absolute local path ngoài Git.");
        for (var parent = new DirectoryInfo(path); parent is not null; parent = parent.Parent)
        {
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0 || (Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git")))) throw new InvalidOperationException("Cache không ở trong repo hoặc reparse point.");
        }
        Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows())
        {
            var sid = WindowsIdentity.GetCurrent().User!; var security = new DirectorySecurity(); security.SetOwner(sid); security.SetAccessRuleProtection(true, false);
            foreach (var owner in new[] { sid, new SecurityIdentifier("S-1-5-18") }) security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);
        }
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string Id(string token) => Hash(CentralId + "|" + token);
    private IDataProtector ViewProtector(string token, string file) => protector.CreateProtector("view", Id(token), file);
    private string MemoryKey(string token, string file) => Id(token) + "|" + file;
    private string DirectoryFor(string token) => Path.Combine(root, Id(token));
    public CachedTicket? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return null;
        var id = Id(token);
        if (tickets.TryGetValue(id, out var cached)) return cached with { Pending = ReadUserPending(cached.Proof.UserId) };
        var path = Path.Combine(root, id, "ticket.bin");
        if (!File.Exists(path)) return null;
        try
        {
            var ticket = JsonSerializer.Deserialize<CachedTicket>(protector.Unprotect(File.ReadAllBytes(path)));
            if (ticket is not null && ticket.CredentialId != id) return null;
            if (ticket is not null) tickets.TryAdd(id, ticket);
            return ticket is null ? null : ticket with { Pending = ReadUserPending(ticket.Proof.UserId) };
        }
        catch (Exception e) when (e is CryptographicException or JsonException or IOException) { return null; }
    }
    public static bool Valid(CachedTicket? t, DateTimeOffset now) => t is { Revoked: false } && t.Proof.ExpiresAt > now && t.Proof.VerifiedAt <= now;
    public async Task SaveProofAsync(string token, CentralProof proof, CancellationToken ct, string? snapshotEpoch = null)
    {
        await writer.WaitAsync(ct);
        try
        {
            var old = Read(token);
            if (old?.Revoked == true || old is not null && (old.Proof.VerifiedAt > proof.VerifiedAt || old.Proof.SecurityVersion > proof.SecurityVersion)) return;
            var changed = old is not null && (old.Proof.AuthorityHash != proof.AuthorityHash || snapshotEpoch is not null && old.SnapshotEpoch != snapshotEpoch);
            var next = new CachedTicket(proof, changed ? "" : old?.ReadyEpoch ?? "", false, changed ? [] : old?.Views ?? [], ReadUserPending(proof.UserId), snapshotEpoch ?? old?.SnapshotEpoch ?? "", changed ? null : old?.Checkpoints, Id(token));
            Save(token, next);
            if (changed) RemoveViews(token, old!.Views);
        }
        finally { writer.Release(); }
    }
    public async Task SaveViewAsync(string token, CentralProof proof, string epoch, string key, byte[] body, string type, string? disposition, DateTimeOffset now, CancellationToken ct, IReadOnlyDictionary<string, string>? dependencies = null, string? snapshotEpoch = null)
    {
        if (body.Length > 26 * 1024 * 1024) return;
        await writer.WaitAsync(ct);
        try
        {
            var old = Read(token);
            if (!Valid(old, now) || old!.Proof.AuthorityHash != proof.AuthorityHash || snapshotEpoch is not null && old.SnapshotEpoch != snapshotEpoch) return;
            var heads = Merge(old.Checkpoints, dependencies);
            var remaining = Compatible(old.Views, heads);
            if (dependencies?.Any(d => heads.TryGetValue(d.Key, out var known) && Older(d.Value, known)) == true)
            { Save(token, old with { Views = remaining.ToArray(), Checkpoints = heads }); RemoveViews(token, old.Views.Where(v => !remaining.Any(n => n.File == v.File))); return; }
            remaining.RemoveAll(v => v.Key == key);
            while (remaining.Count >= 200 || remaining.Sum(v => (long)v.Size) + body.Length > MaxBytes) remaining.RemoveAt(0);
            var file = Guid.NewGuid().ToString("N") + ".view";
            Atomic(Path.Combine(DirectoryFor(token), file), ViewProtector(token, file).Protect(body));
            remaining.Add(new(key, file, type, disposition, now, body.Length, dependencies?.ToDictionary(p => p.Key, p => p.Value), Presentation.CachedHtmlPresentation.IsHtml(type) ? Presentation.WebUi.CacheRevision : null));
            Save(token, old with { ReadyEpoch = epoch, Views = remaining.ToArray(), Checkpoints = heads });
            RemoveViews(token, old.Views.Where(v => !remaining.Any(n => n.File == v.File)));
        }
        finally { writer.Release(); }
    }
    public async Task ObserveChangesAsync(string token, IReadOnlyDictionary<string, string> dependencies, CancellationToken ct)
    {
        await writer.WaitAsync(ct);
        try
        {
            if (Read(token) is not { } old) return;
            var heads = Merge(old.Checkpoints, dependencies); var views = Compatible(old.Views, heads);
            Save(token, old with { ReadyEpoch = "", Views = views.ToArray(), Checkpoints = heads });
            RemoveViews(token, old.Views.Where(v => !views.Any(n => n.File == v.File)));
        }
        finally { writer.Release(); }
    }
    private static bool Older(string incoming, string known) => long.TryParse(incoming.Split('|')[0], out var a) && long.TryParse(known.Split('|')[0], out var b) && a < b;
    private static Dictionary<string, string> Merge(Dictionary<string, string>? old, IReadOnlyDictionary<string, string>? incoming)
    {
        var heads = old?.ToDictionary(p => p.Key, p => p.Value) ?? new();
        if (incoming is not null) foreach (var d in incoming) if (!heads.TryGetValue(d.Key, out var previous) || !Older(d.Value, previous)) heads[d.Key] = d.Value;
        return heads;
    }
    private static List<CachedView> Compatible(IEnumerable<CachedView> views, Dictionary<string, string> heads) => views.Where(v => heads.Count == 0 || v.Dependencies is { Count: > 0 } && !heads.Any(h => v.Dependencies.TryGetValue(h.Key, out var previous) && previous != h.Value)).ToList();
    public byte[]? Bytes(string token, CachedView view)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(view.File, "^[a-f0-9]{32}\\.view$")) return null;
        try {
            var path = Path.Combine(DirectoryFor(token), view.File);
            if (!File.Exists(path)) { memory.Remove(MemoryKey(token, view.File)); return null; }
            if (memory.TryGetValue<byte[]>(MemoryKey(token, view.File), out var hit)) return hit;
            var b = ViewProtector(token, view.File).Unprotect(File.ReadAllBytes(path));
            if (b.Length != view.Size) return null;
            memory.Set(MemoryKey(token, view.File), b, new MemoryCacheEntryOptions { Size = b.Length, SlidingExpiration = TimeSpan.FromMinutes(10) }); return b;
        }
        catch (Exception e) when (e is CryptographicException or IOException) { return null; }
    }
    public async Task UpdatePendingAsync(string token, PendingWrite? pending, CancellationToken ct)
    {
        await writer.WaitAsync(ct);
        try
        {
            if (Read(token) is not { } ticket) return;
            var user = ticket.Proof.UserId; var path = UserPendingPath(user); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Atomic(path, protector.Protect(JsonSerializer.SerializeToUtf8Bytes(new UserPending(pending))));
            userPending[user] = new(pending); Save(token, ticket with { Pending = pending });
        }
        finally { writer.Release(); }
    }
    private string UserPendingPath(string user) => Path.Combine(root, "user-" + Hash(user), "pending.bin");
    private PendingWrite? ReadUserPending(string user)
    {
        if (userPending.TryGetValue(user, out var hit)) return hit.Value;
        var path = UserPendingPath(user);
        var pending = File.Exists(path) ? JsonSerializer.Deserialize<UserPending>(protector.Unprotect(File.ReadAllBytes(path))) ?? throw new CryptographicException("Invalid write intent") : new UserPending(null);
        userPending.TryAdd(user, pending); return pending.Value;
    }
    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        await writer.WaitAsync(ct);
        try { var old = Read(token); if (old is null) return; Save(token, old with { Revoked = true, ReadyEpoch = "", Views = [] }); RemoveViews(token, old.Views); }
        finally { writer.Release(); }
    }
    public async Task NotReadyAsync(string token, CancellationToken ct) => await UpdateAsync(token, t => t with { ReadyEpoch = "" }, ct);
    private async Task UpdateAsync(string token, Func<CachedTicket, CachedTicket> edit, CancellationToken ct)
    {
        await writer.WaitAsync(ct); try { if (Read(token) is { } old) Save(token, edit(old)); } finally { writer.Release(); }
    }
    private void Save(string token, CachedTicket ticket)
    {
        var directory = DirectoryFor(token); Directory.CreateDirectory(directory);
        Atomic(Path.Combine(directory, "ticket.bin"), protector.Protect(JsonSerializer.SerializeToUtf8Bytes(ticket)));
        tickets[Id(token)] = ticket;
    }
    private static void Atomic(string path, byte[] bytes)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(bytes); f.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private void RemoveViews(string token, IEnumerable<CachedView> views)
    {
        foreach (var v in views) if (System.Text.RegularExpressions.Regex.IsMatch(v.File, "^[a-f0-9]{32}\\.view$")) { var path = Path.Combine(DirectoryFor(token), v.File); if (File.Exists(path)) File.Delete(path); memory.Remove(MemoryKey(token, v.File)); memory.Remove("index:" + MemoryKey(token, v.File)); }
    }
    public string SearchText(string token, CachedView view)
    {
        if (memory.TryGetValue<string>("index:" + MemoryKey(token, view.File), out var found)) return found!;
        var bytes = Bytes(token, view); if (bytes is null) return "";
        var text = Encoding.UTF8.GetString(bytes);
        if (view.ContentType.Contains("json"))
        {
            try { using var doc = JsonDocument.Parse(bytes); var fields = new List<string>(); Flatten(doc.RootElement, fields); text = string.Join(" ", fields); }
            catch (JsonException) { return ""; }
        }
        else text = Presentation.CachedHtmlPresentation.Text(bytes);
        text = Normalize(text); memory.Set("index:" + MemoryKey(token, view.File), text, new MemoryCacheEntryOptions { Size = text.Length * 2L, SlidingExpiration = TimeSpan.FromMinutes(10) }); return text;
    }
    private static void Flatten(JsonElement value, List<string> fields)
    { if (value.ValueKind == JsonValueKind.Object) foreach (var p in value.EnumerateObject()) Flatten(p.Value, fields); else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Flatten(item, fields); else if (value.ValueKind is JsonValueKind.String or JsonValueKind.Number) fields.Add(value.ToString()); }
    public static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)).Replace('đ', 'd').Replace('Đ', 'D').ToUpperInvariant();
    public void Dispose() { lease.Dispose(); writer.Dispose(); memory.Dispose(); }
}
