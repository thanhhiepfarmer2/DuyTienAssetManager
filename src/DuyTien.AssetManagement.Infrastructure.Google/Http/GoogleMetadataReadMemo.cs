using System.Collections.Concurrent;
using System.Net;

namespace DuyTien.AssetManagement.Infrastructure.Google.Http;

// Metadata only, per request. Values/file bodies/auth never enter this memo.
// The existing coordinator still supplies business snapshot consistency.
public static class GoogleMetadataReadMemo
{
    private sealed record Packet(byte[] Body, KeyValuePair<string, string[]>[] Headers, KeyValuePair<string, string[]>[] ContentHeaders)
    {
        public HttpResponseMessage Response()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Body) };
            foreach (var h in Headers) response.Headers.TryAddWithoutValidation(h.Key, h.Value);
            foreach (var h in ContentHeaders) response.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            return response;
        }
    }
    private sealed class Frame { public readonly ConcurrentDictionary<string, Packet> Responses = new(); public long Revision; public readonly object Sync = new(); }
    private static readonly AsyncLocal<Frame?> Current = new();
    public static IDisposable Begin(bool readRequest)
    { var old = Current.Value; Current.Value = readRequest ? new() : null; return new Reset(() => Current.Value = old); }
    public static long Revision => Current.Value?.Revision ?? -1;
    public static void Invalidate() { if (Current.Value is { } frame) { lock (frame.Sync) { Interlocked.Increment(ref frame.Revision); frame.Responses.Clear(); } } }
    private static bool Metadata(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri is not { } uri) return false;
        var marker = uri.AbsolutePath.IndexOf("/spreadsheets/", StringComparison.Ordinal);
        if (marker < 0 || uri.AbsolutePath.Length <= marker + 14 || uri.AbsolutePath[(marker + 14)..].Contains('/')) return false;
        var fields = Uri.UnescapeDataString(uri.Query);
        return fields.Contains("fields=sheets.properties", StringComparison.Ordinal) && !fields.Contains("includeGridData=true", StringComparison.OrdinalIgnoreCase);
    }
    public static HttpResponseMessage? Read(HttpRequestMessage request)
    {
        if (Current.Value is not { } frame || !Metadata(request)) return null;
        return frame.Responses.TryGetValue(request.RequestUri!.AbsoluteUri, out var packet) ? packet.Response() : null;
    }
    public static async Task StoreAsync(HttpRequestMessage request, HttpResponseMessage response, long expectedRevision, CancellationToken ct)
    {
        if (Current.Value is not { } frame || !Metadata(request) || response.StatusCode != HttpStatusCode.OK || frame.Responses.Count >= 8) return;
        // Metadata requests explicitly exclude grid data in production stores.
        if (request.RequestUri!.Query.Contains("includeGridData=true", StringComparison.OrdinalIgnoreCase)) return;
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > 1024 * 1024 || frame.Revision != expectedRevision) return;
        lock (frame.Sync)
        {
            if (frame.Revision != expectedRevision) return;
            frame.Responses[request.RequestUri.AbsoluteUri] = new(bytes, response.Headers.Select(h => new KeyValuePair<string, string[]>(h.Key, h.Value.ToArray())).ToArray(), response.Content.Headers.Select(h => new KeyValuePair<string, string[]>(h.Key, h.Value.ToArray())).ToArray());
        }
    }
    private sealed class Reset(Action reset) : IDisposable { public void Dispose() => reset(); }
}
